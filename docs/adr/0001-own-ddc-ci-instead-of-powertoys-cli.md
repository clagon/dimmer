# DDC/CI・WMI を自前で叩き、PowerToys PowerDisplay CLI のラッパーにはしない

PowerDisplay CLI をラップすれば最小コストで Shortcut 機能を作れるが、PowerToys の常駐が必須になること、1 Setting ごとにプロセス起動が必要で Apply が遅いこと、Monitor の識別が CLI の stable ID に縛られ後から自前実装へ移ると保存済み Profile が壊れることから、最初から `dxva2.dll`（DDC/CI）と WMI（内蔵パネル）を直接使う。v1 はスライダー UI を持たず、その間は PowerDisplay との同時起動を許容する（値の食い違いは Apply 時の再読み込みで吸収）。v2 で自前フライアウトを持った時点で共存は非サポートとする。
