

## ファイル指定書式

**利用可能なパラメータ:**
- {frame} : フレーム番号
- {frame:0} : フレーム番号（0埋めなし）
- {frame:n} : フレーム番号（0埋めあり、桁数n）
- {time} : 時間（hh:mm:ss.mmm）
- {timecode} : 時間コード（hh:mm:ss:ff）

**例:**
- `output_{frame:0}.png` : `output_0.png`, `output_1.png`, ...
- `output_{frame:4}.png` : `output_0000.png`, `output_0001.png`, ...
- `output_{time}.png` : `output_00:00:00.000.png`, `output_00:00:01.000.png`, ...
- `output_{timecode}.png` : `output_00:00:00:00.png`, `output_00:00:01:00.png`, ...
