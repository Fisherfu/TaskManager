# NPC 互動對話 — Unity 最小可玩原型

這個資料夾只包含 C# 腳本，沒有完整的 Unity 專案檔（`Library/`、`ProjectSettings/` 等），
因為這些是 Unity Editor 開啟專案時自動產生的二進位/快取內容，不適合手動生成。
請照下面步驟，5–10 分鐘內就能在 Unity Editor 裡組出可玩畫面。

## 需求
- Unity 2022 LTS 或更新版本（Unity 6 也可以）
- 建立專案時選擇 **2D (URP 或 Built-in 皆可)** 範本

## 1. 匯入腳本
把 `Assets/Scripts` 整個資料夾複製到你新建的 Unity 專案的 `Assets/` 底下。

## 2. 匯入 TextMeshPro
腳本用了 TMP_Text。第一次使用時 Unity 會跳出提示，點選
**Window > TextMeshPro > Import TMP Essential Resources**，全部用預設值匯入即可。

## 3. 建立圖層（Layer）
`Edit > Project Settings > Tags and Layers`，新增一個 Layer 叫 `Interactable`。

## 4. 場景物件

### Player
1. 建立空 GameObject `Player`，加上：
   - `Sprite Renderer`（隨便一個方塊/角色貼圖）
   - `Rigidbody2D`（Gravity Scale = 0，Constraints 鎖 Rotation Z）
   - `Collider2D`（例如 CapsuleCollider2D）
   - `PlayerController` 腳本
   - `PlayerInteractor` 腳本
2. `PlayerInteractor` 的 `Interactable Layer` 勾選剛剛建立的 `Interactable`。

### NPC
1. 建立空 GameObject `NPC_XXX`，加上：
   - `Sprite Renderer`
   - `Rigidbody2D`（Gravity Scale = 0，Body Type 可設為 Kinematic 或鎖 Rotation）
   - `Collider2D`
   - Layer 設為 `Interactable`
   - `NPCController` 腳本
   - `NPCDialogueTrigger` 腳本
2.（可選）在 NPC 旁建立 2–4 個空物件當作巡邏點 `Waypoint_1`, `Waypoint_2`...，
   拖進 `NPCController` 的 `Waypoints` 陣列。不設定的話 NPC 會保持原地 Idle。

### 對話資料
右鍵 Project 視窗 → `Create > NPC Game > Dialogue Data`，
填入 `npcName` 與 `lines`（每行一句對白），
把這個 asset 拖進 NPC 的 `NPCDialogueTrigger` → `Dialogue Data` 欄位。

### Dialogue UI
1. 建立 `Canvas`（Screen Space - Overlay）。
2. 在 Canvas 下建立一個面板 `DialoguePanel`（放螢幕下方），裡面放：
   - `SpeakerNameText`（TMP Text）
   - `DialogueText`（TMP Text）
3. 建立空 GameObject `DialogueManager`，加上 `DialogueManager` 腳本，
   把 `DialoguePanel`、`SpeakerNameText`、`DialogueText` 拖進對應欄位。
   `DialoguePanel` 預設會被腳本設成不啟用，不用手動關閉。

### 互動提示（"按 E 說話"）
1. 建立 World Space 的小 Canvas `InteractionPrompt`（縮放調小，例如 0.01），
   裡面放一個 TMP Text。
2. 加上 `InteractionPrompt` 腳本，把 Canvas 本身拖進 `Prompt Root`，
   文字物件拖進 `Prompt Text`。
3. 把這個 `InteractionPrompt` 物件拖進 `Player` 的 `PlayerInteractor` → `Prompt` 欄位
   （建議設成 DontDestroyOnLoad 或直接放場景根層即可，原型階段不用太講究）。

## 5. 測試
按 Play，方向鍵/WASD 移動玩家，靠近 NPC 會出現「Talk to XXX」提示，
按 `E` 開始對話，對話中再按 `E` 逐句推進或跳過打字機效果，
對話結束後 NPC 恢復巡邏、玩家恢復可移動。

## 之後可以擴充的方向
- 對話分支選項（在 `DialogueData` 加入 choices，`DialogueManager` 顯示按鈕）
- NPC 好感度/任務系統
- 存檔（NPC 對話進度、任務狀態）
- 用 Cinemachine 做鏡頭跟隨

## 部署測試

想讓別人透過瀏覽器連結試玩，請看 [`DEPLOY.md`](./DEPLOY.md)
（GitHub Actions 自動建置 WebGL 並發布到 GitHub Pages）。
