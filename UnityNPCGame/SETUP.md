# NPC 互動對話 — Unity 最小可玩原型

2D 俯視角原型：玩家在房間裡移動，靠近 NPC 按 `E` 觸發對話，NPC 會沿著巡邏點走動，
對話進行中會停下，結束後恢復巡邏。

## 需求
- Unity **2022.3 LTS**（`ProjectSettings/ProjectVersion.txt` 目前指定 2022.3.0f1，
  用你已安裝的 2022.3.x 開啟即可，Unity 會自動更新這個檔案）
- 如果要建置 WebGL，安裝 Unity 時要勾選 **WebGL Build Support** 模組

## 開起來就能玩

1. Unity Hub → **Add project from disk** → 選 `UnityNPCGame/` 資料夾
2. 第一次開啟時，`Assets/Editor/SceneBuilder.cs` 會自動產生場景與 Prefab
   （Console 會出現 `[SceneBuilder] Generated Assets/Scenes/Main.unity`）
3. 打開 `Assets/Scenes/Main.unity`，按 **Play**

操作：`WASD` / 方向鍵移動，靠近 NPC 出現提示後按 `E` 開始對話，
對話中按 `E` 跳過打字機效果或推進下一句。

> 產生器只在 `Assets/Scenes/Main.unity` **不存在** 時才會跑，所以你之後在 Editor 裡
> 手動調整場景不會被覆蓋。想重新產生一份乾淨的，選單 **NPC Game > Rebuild Sample Scene**
> （會覆蓋現有場景，執行前會先跳確認視窗）。

## 自動產生的內容

| 路徑 | 內容 |
|---|---|
| `Assets/Scenes/Main.unity` | 主場景：攝影機、房間（地板＋四面牆碰撞）、玩家、3 個 NPC、UI |
| `Assets/Prefabs/Player.prefab` | 玩家：SpriteRenderer + Rigidbody2D + BoxCollider2D + 兩支控制腳本 |
| `Assets/Prefabs/NPC.prefab` | NPC：CircleCollider2D、Interactable 圖層、狀態機 + 對話觸發器 |
| `Assets/Prefabs/DialogueSystem.prefab` | 對話 UI（Canvas + 面板 + TMP 文字）與 `DialogueManager` |
| `Assets/Prefabs/InteractionPrompt.prefab` | 世界座標的「Press E」提示 |
| `Assets/Dialogue/*.asset` | 三個 NPC 的對白資料（ScriptableObject） |
| `Assets/Art/*.png` | 程式產生的方形／圓形佔位圖 |

NPC 的 `dialogueData` 與巡邏點是**逐一實例設定**（prefab override），
所以你可以直接複製 `NPC.prefab` 再指定不同對白與路線。

## 腳本結構

| 腳本 | 職責 |
|---|---|
| `Player/PlayerController` | 8 方向移動，對話時由 `InputLocked` 鎖住 |
| `Player/PlayerInteractor` | 以 `OverlapCircleAll` 找最近的 `IInteractable`，按鍵觸發 |
| `Interaction/IInteractable` | 互動介面（提示文字 + `Interact`） |
| `NPC/NPCController` | Idle / Patrol / Talking 狀態機，巡邏點循環與停留 |
| `NPC/NPCDialogueTrigger` | 實作 `IInteractable`，開對話並鎖住雙方，結束時回復 |
| `Dialogue/DialogueData` | ScriptableObject：NPC 名稱 + 對白陣列 |
| `Dialogue/DialogueManager` | 單例，打字機效果、逐句推進、結束回呼 |
| `UI/InteractionPrompt` | 世界座標提示的顯示／隱藏與定位 |

## 想改成中文對白？

預設的 TMP 字型（LiberationSans）**不含中文字**，直接把對白改成中文會顯示空白或方框。
要用中文請這樣做：

1. 準備一個含中文的字型檔（例如 Google 的 Noto Sans TC），放進 `Assets/Fonts/`
2. `Window > TextMeshPro > Font Asset Creator`，來源選該字型，
   Character Set 選 **Custom Characters** 並貼上你會用到的中文字，然後 Generate + Save
3. 把產生的 Font Asset 指定給 `DialogueSystem` prefab 裡的 TMP 文字元件
   （或設成 `Edit > Project Settings > TextMeshPro > Default Font Asset`）
4. 接著就能把 `Assets/Dialogue/*.asset` 的內容換成中文

> 中文字型的字集很大，記得用 Custom Characters 只打包用得到的字，否則 WebGL 體積會爆增。

## 之後可以擴充的方向
- 對話分支選項（在 `DialogueData` 加入 choices，`DialogueManager` 顯示按鈕）
- NPC 好感度／任務系統
- 存檔（NPC 對話進度、任務狀態）
- 用 Cinemachine 做鏡頭跟隨（目前是固定攝影機，剛好框住整個房間）

## 部署測試

想讓別人透過瀏覽器連結試玩，請看 [`DEPLOY.md`](./DEPLOY.md)
（GitHub Actions 自動建置 WebGL 並發布到 GitHub Pages）。
