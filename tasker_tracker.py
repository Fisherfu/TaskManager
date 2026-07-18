import json
import re
import os
import ssl
import smtplib
from email.mime.text import MIMEText
from datetime import datetime, timezone, timedelta
import requests
from bs4 import BeautifulSoup

LIST_URL = "https://www.tasker.com.tw/cases?selected_categories=110,101"
STATE_FILE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "tasker_seen_cases.json")
HEADERS = {
    "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
}

NOTIFY_EMAIL = "fuyuangche@gmail.com"


class _RelaxedStrictnessAdapter(requests.adapters.HTTPAdapter):
    """tasker.com.tw's cert chain lacks a Subject Key Identifier extension,
    which OpenSSL 3's strict X.509 chain-building rejects even though the
    chain is otherwise valid (confirmed via curl/schannel). This only
    loosens that one RFC-5280 strictness flag; full chain/hostname
    verification stays on."""

    def init_poolmanager(self, *args, **kwargs):
        ctx = ssl.create_default_context()
        if hasattr(ctx, "verify_flags") and hasattr(ssl, "VERIFY_X509_STRICT"):
            ctx.verify_flags &= ~ssl.VERIFY_X509_STRICT
        kwargs["ssl_context"] = ctx
        return super().init_poolmanager(*args, **kwargs)


def make_session():
    s = requests.Session()
    s.mount("https://", _RelaxedStrictnessAdapter())
    s.headers.update(HEADERS)
    return s


SESSION = make_session()

# 使用者鎖定的三大關注方向：網站開發(前後台皆可)、數據分析、AI應用
KEYWORD_GROUPS = {
    "web": ["網頁", "網站", "前端", "後端", "全端", "shopify", "wordpress",
            "電商", "後台管理", "rwd", "django", "react", "vue", "node.js", "app開發"],
    "data": ["數據分析", "資料分析", "data analysis", "儀表板", "報表系統",
             "數據視覺化", "資料視覺化", "資料庫設計", "etl"],
    "ai": ["人工智慧", "機器學習", "深度學習", "llm", "rag架構", "comfyui",
           "gpt", "chatbot", "聊天機器人", "電腦視覺", "ai應用", "ai整合", "ai模型", "ai開發"],
}

# 出現這些詞代表主要是硬體/韌體/嵌入式案件，即使誤中上面關鍵字也應排除
HARDWARE_EXCLUDE_KEYWORDS = [
    "stm32", "pcb", "mcu", "firmware", "韌體", "電路板", "單晶片",
    "arduino", "fpga", "plc", "嵌入式",
]

MAX_NEW_CASES_PER_RUN = 15


def load_seen_state():
    if os.path.exists(STATE_FILE):
        with open(STATE_FILE, "r", encoding="utf-8") as f:
            return json.load(f)
    return {}


def save_seen_state(state):
    with open(STATE_FILE, "w", encoding="utf-8") as f:
        json.dump(state, f, ensure_ascii=False, indent=2)


def fetch_tasker_cases():
    print(f"Fetching cases from Tasker: {LIST_URL}...")
    try:
        response = SESSION.get(LIST_URL, timeout=15)
        response.raise_for_status()
    except Exception as e:
        print(f"Error fetching main page: {e}")
        return []

    soup = BeautifulSoup(response.text, "html.parser")
    cases = []

    json_ld_tags = soup.find_all("script", type="application/ld+json")
    for tag in json_ld_tags:
        try:
            data = json.loads(tag.string)
            objs = data if isinstance(data, list) else [data]
            for item_obj in objs:
                if item_obj.get("@type") == "ItemList":
                    for el in item_obj.get("itemListElement", []):
                        item = el.get("item", {})
                        if item.get("@type") == "CreativeWork":
                            cases.append({"title": item.get("name"), "url": item.get("url")})
        except Exception:
            continue

    if not cases:
        print("JSON-LD not found, using HTML link parsing...")
        seen_urls = set()
        for link in soup.find_all("a", href=re.compile(r"/cases/TK")):
            title_text = link.get_text(strip=True)
            href = link.get("href")
            full_url = href if href.startswith("http") else f"https://www.tasker.com.tw{href}"
            if title_text and full_url not in seen_urls:
                seen_urls.add(full_url)
                cases.append({"title": title_text, "url": full_url})

    return cases


def fetch_case_detail(case_url):
    try:
        response = SESSION.get(case_url, timeout=10)
        response.raise_for_status()
    except Exception as e:
        print(f"Error fetching detail {case_url}: {e}")
        return {"description": "無法讀取細節內容", "budget": "面議", "location": "可遠端"}

    soup = BeautifulSoup(response.text, "html.parser")

    json_ld_tags = soup.find_all("script", type="application/ld+json")
    for tag in json_ld_tags:
        try:
            data = json.loads(tag.string)
            objs = data if isinstance(data, list) else [data]
            for obj in objs:
                if obj.get("@type") == "JobPosting":
                    return {
                        "description": (obj.get("description") or "").strip(),
                        "budget": "預算詳談",
                        "location": "可遠端",
                    }
        except Exception:
            continue

    desc = ""
    desc_meta = soup.find("meta", attrs={"name": "description"})
    if desc_meta:
        desc = desc_meta.get("content", "")

    return {
        "description": desc.strip() if desc else "詳見網站需求說明",
        "budget": "預算詳談",
        "location": "可遠端",
    }


def matched_keyword_groups(case):
    text = (case.get("title", "") + " " + case.get("description", "")).lower()
    if any(kw in text for kw in HARDWARE_EXCLUDE_KEYWORDS):
        return []
    matched = [group for group, kws in KEYWORD_GROUPS.items() if any(kw in text for kw in kws)]
    return matched


def build_strategy_and_template(case, groups):
    title = case["title"]
    if "ai" in groups:
        strategy = "業主有 AI 相關開發或套件整合需求。提案應著重於 AI 應用場景的經驗、模型部署及微調能力。"
        template = (
            f"您好，我是 Fisherfu 開發團隊，專注於 AI 整合與系統研發。針對您的「{title}」需求，我們能提供：\n"
            "1. AI 核心功能整合與套件客製（如 ComfyUI/LLM/RAG 架構部署）。\n"
            "2. 串接優化：提供高併發、低延遲的 API 串接，並進行本地伺服器效能調優。\n"
            "3. 生產環境部署與一年系統 Bug 保固。\n"
            "隨信附上我們的作品集與簡介，期待能與您進一步通話討論細節！"
        )
    elif "data" in groups:
        strategy = "業主有數據分析/報表視覺化需求。提案應著重於資料庫設計、ETL 與儀表板呈現能力。"
        template = (
            f"您好，我是 Fisherfu 開發團隊，擅長資料庫架構設計與數據分析。針對您的「{title}」需求，我們能提供：\n"
            "1. 資料庫 Schema 設計與 ETL 流程建置，確保數據乾淨可追溯。\n"
            "2. 客製化儀表板與報表視覺化（BI 工具或自建前端圖表）。\n"
            "3. 提供分析邏輯文件與後續維運教學。\n"
            "期待能與您進一步討論資料來源與分析目標！"
        )
    elif "web" in groups:
        strategy = "業主有網頁系統開發或電商建置需求。提案應強調我們已具備 Django 短租管理系統等現成系統原型，能快速上線。"
        template = (
            f"您好，我是 Fisherfu 開發團隊，專注於客製化網頁系統與電商建置。針對您的「{title}」，我們能為您提供：\n"
            "1. 客製化網頁與後台管理系統（採用企業級 Django 框架，內建高安全防護）。\n"
            "2. PC/平板響應式介面（RWD）設計，前後台皆可負責。\n"
            "3. 部署於雲端平台，實現一鍵自動化部署，並提供 1 年保固。\n"
            "我們已有多個現成網頁管理系統原型，能為您快速客製並提早上線，期待與您洽談！"
        )
    else:
        strategy = "通用軟硬體/系統開發案件，與目標技能關聯較弱，建議評估後再決定是否投案。"
        template = (
            f"您好，我們是 Fisherfu 開發團隊。針對您的「{title}」專案，我們能為您提供專業的系統規劃與開發服務：\n"
            "1. 完整的需求分析、資料庫 Schema 設計與前後端全端開發。\n"
            "2. 嚴謹的系統測試與效能調校，確保運作高可用與資安防護。\n"
            "3. 提供完整技術移轉手冊、原始碼交付及 1 年 Bug 保固。\n"
            "期待有機會與您進一步通話討論，謝謝！"
        )
    return strategy, template


def generate_report(cases_data):
    now_taipei = datetime.now(timezone(timedelta(hours=8)))
    md_content = "# Tasker 新案件追蹤與投案建議\n\n"
    md_content += f"更新時間: {now_taipei.strftime('%Y-%m-%d %H:%M')} (Asia/Taipei)\n\n"

    if not cases_data:
        md_content += "本次執行沒有發現符合條件的新案件。\n"
        return md_content

    for idx, case in enumerate(cases_data, 1):
        strategy, template = build_strategy_and_template(case, case["matched_groups"])
        md_content += f"## {idx}. {case['title']}\n"
        md_content += f"- **案件連結**: [{case['url']}]({case['url']})\n"
        md_content += f"- **執行地點**: {case['location']}\n"
        md_content += f"- **預算金額**: {case['budget']}\n"
        md_content += f"- **命中分類**: {', '.join(case['matched_groups']) or '無(未命中關鍵字，僅供參考)'}\n\n"
        md_content += "### 需求說明\n"
        md_content += f"```text\n{case['description']}\n```\n\n"
        md_content += "### 建議投案策略 & 提案信範本\n"
        md_content += f"**開發策略**：{strategy}\n\n"
        md_content += f"**提案信範本**：\n```text\n{template}\n```\n\n"
        md_content += "---\n\n"

    return md_content


def send_email_notification(cases_data):
    app_password = os.environ.get("GMAIL_APP_PASSWORD")
    if not app_password:
        print("GMAIL_APP_PASSWORD not set, skipping email notification (this is expected for local test runs).")
        return

    now_taipei = datetime.now(timezone(timedelta(hours=8)))
    subject = f"Tasker 新案件通知 {now_taipei.strftime('%Y-%m-%d %H:%M')} - {len(cases_data)} 筆符合條件的新案件"
    body = generate_report(cases_data)

    msg = MIMEText(body, "plain", "utf-8")
    msg["Subject"] = subject
    msg["From"] = NOTIFY_EMAIL
    msg["To"] = NOTIFY_EMAIL

    try:
        with smtplib.SMTP("smtp.gmail.com", 587, timeout=15) as server:
            server.starttls(context=ssl.create_default_context())
            server.login(NOTIFY_EMAIL, app_password)
            server.send_message(msg)
        print(f"Sent email notification for {len(cases_data)} new case(s).")
    except Exception as e:
        print(f"Error sending email notification: {e}")


def main():
    seen_state = load_seen_state()
    all_cases = fetch_tasker_cases()
    if not all_cases:
        print("No cases found or failed to fetch listing page.")
        return

    print(f"Found {len(all_cases)} cases on listing page. Checking against seen state ({len(seen_state)} known)...")

    unseen = [c for c in all_cases if c["url"] not in seen_state]
    print(f"{len(unseen)} cases are new since last run.")

    relevant_new_cases = []
    for c in unseen[:MAX_NEW_CASES_PER_RUN]:
        print(f"Fetching detail: {c['title']}...")
        details = fetch_case_detail(c["url"])
        c.update(details)
        groups = matched_keyword_groups(c)
        c["matched_groups"] = groups
        seen_state[c["url"]] = {
            "title": c["title"],
            "first_seen": datetime.now(timezone.utc).isoformat(),
            "relevant": bool(groups),
        }
        if groups:
            relevant_new_cases.append(c)

    save_seen_state(seen_state)
    print(f"Saved state ({len(seen_state)} total known cases) to {STATE_FILE}")

    json_path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "tasker_latest_cases.json")
    with open(json_path, "w", encoding="utf-8") as f:
        json.dump(relevant_new_cases, f, ensure_ascii=False, indent=2)
    print(f"Saved {len(relevant_new_cases)} relevant new cases to {json_path}")

    md_content = generate_report(relevant_new_cases)
    md_path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "tasker_latest_cases.md")
    with open(md_path, "w", encoding="utf-8") as f:
        f.write(md_content)
    print(f"Saved Markdown report to {md_path}")

    if relevant_new_cases:
        send_email_notification(relevant_new_cases)


if __name__ == "__main__":
    main()
