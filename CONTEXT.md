# Dimmer

タスクバーに常駐し、Windows に接続されたモニターの設定（明るさ・入力ソースなど）を操作するアプリ。保存した設定の組み合わせをショートカットで一発適用できる。

## Language

### モニターと設定

**Monitor**:
Windows に接続された 1 台の表示装置。外部モニターも内蔵パネルも含み、Setting を操作できないもの（DDC/CI 非対応など）も含む。
_Avoid_: Display, 画面

**Monitor Name**:
ユーザーが Monitor に付ける呼び名。未設定なら型番名（同型が複数あれば番号付き）で代用される。
_Avoid_: Alias, Label, ニックネーム

**Setting**:
Monitor が持つ操作可能な 1 項目（明るさ・コントラスト・音量・入力ソース・Orientation・色温度・電源状態）。明るさ・コントラスト・音量は 0〜100% で表し、それ以外は選択肢から選ぶ。
_Avoid_: Control, パラメータ

**Orientation**:
Monitor の表示の向き（0/90/180/270 度）。
_Avoid_: Rotation, 回転（Profile Cycle と紛らわしいため）

### プロファイル

**Profile**:
「どの Monitor のどの Setting をどの値にするか」の部分的な指定の集まり。指定されていない Setting には触れない。
_Avoid_: Preset, Snapshot

**Apply**:
Profile に含まれる指定を実際の Monitor に反映すること。見つからない Monitor や、その Monitor が対応していない Setting はスキップされる。
_Avoid_: Load, 切り替え

**Profile Cycle**:
名前付きの、2 つ以上の Profile を重複なく並べたもの。Shortcut を押すたびに、直近に Apply された Profile の次を Apply する（末尾の次は先頭、並びに無ければ先頭）。
_Avoid_: Rotation, Toggle, ローテ

**Adjustment**:
特定の Setting を現在値から一定量だけ増減させる相対操作。Profile には含まれない。
_Avoid_: Step, 相対 Profile

**Cursor Monitor**:
Adjustment の対象指定の一つで、Shortcut を押した瞬間にマウスカーソルが乗っている Monitor。
_Avoid_: Active monitor, Focused monitor

**Shortcut**:
Profile・Profile Cycle・Adjustment のいずれか 1 つに割り当てるグローバルなキーの組み合わせ。
_Avoid_: Hotkey, キーバインド

### フィードバック

**Overlay**:
Shortcut 実行時に一時的に表示される、対象と結果（値や Profile 名）を示す小さな表示。
_Avoid_: OSD, Toast, 通知
