# 部署到 GitHub Pages（WebGL）

CI 設定檔已經放好：`.github/workflows/unity-webgl-deploy.yml`。
它會在你 push 到 `main` 分支且 `UnityNPCGame/**` 有變動時，自動用
[game-ci/unity-builder](https://game.ci/) 建置 WebGL，並發布到 GitHub Pages。

在它能真正跑起來之前，還需要完成三件事，其中前兩件**只有你能做**
（涉及你自己的 Unity 帳號憑證，我不會、也不應該幫你操作）：

## 1. 取得 Unity Personal 授權檔（一次性）

Unity 在 CI 環境跑 Editor 需要授權。免費做法：

1. 本機安裝 Unity Editor 後，在終端機執行：
   ```
   Unity -batchmode -createManualActivationFile -logFile -
   ```
   會產生一個 `Unity_v20XX.X.XXXX_XXX.alf` 檔案。
2. 打開 https://license.unity3d.com/manual ，上傳這個 `.alf` 檔，
   選擇 **Unity Personal**，下載回傳的 `.ulf` 授權檔。
3. 用文字編輯器打開 `.ulf`，複製全部內容。

## 2. 在 GitHub 設定 Secrets

到 repo 的 `Settings > Secrets and variables > Actions`，新增：

| Secret 名稱 | 內容 |
|---|---|
| `UNITY_EMAIL` | 你的 Unity 帳號 email |
| `UNITY_PASSWORD` | 你的 Unity 帳號密碼 |
| `UNITY_LICENSE` | 剛剛下載的 `.ulf` 檔完整內容 |

## 3. 啟用 GitHub Pages

`Settings > Pages` → Source 選擇 **GitHub Actions**。

## 4. 補完 Unity 專案本體

目前 repo 裡只有 `Assets/Scripts`（純 C# 邏輯），CI 需要一個**完整可建置的 Unity
專案**才會成功，也就是照 [`SETUP.md`](./SETUP.md) 在本機 Unity Editor 裡：

- 建好場景（Player、NPC、Dialogue UI 等 GameObject）並存檔到
  `Assets/Scenes/Main.unity`
- 把這個場景加進 `File > Build Settings > Scenes In Build`
- `File > Build Settings > Platform` 切到 **WebGL**，並在本機先手動 Build 一次，
  確認沒有錯誤（WebGL 需要額外安裝 Build Support 模組）
- 把整個專案（含 `ProjectSettings/`、`Packages/`、`Assets/`，`Library/` 不用，
  已經在 `.gitignore` 排除）commit 並 push 到 `main`

完成以上四步後，之後每次 push 有改動，CI 就會自動重新建置並更新
`https://<你的帳號>.github.io/TaskManager/` 這個可玩連結。

## 想先快速手動測試（不等 CI）

在本機 Unity Editor `File > Build Settings > WebGL > Build`，
建出來的資料夾可以：
- 拖到 [itch.io](https://itch.io) 上傳當作 HTML5 遊戲，幾分鐘就有連結可分享
- 或本機用 `python3 -m http.server` 在 build 資料夾內起個伺服器本地測試
  （WebGL build 不能直接雙擊 index.html 開，瀏覽器會擋 CORS）
