# BetterChineseNames（更好的中文译名）

本 Mod 会对官方译名进行调整，使之更符合中国本土的风格。同时也支持自定义翻译。

## 名字类

- 地名：城市区划、道路、桥、坝的名字
- 人名：男女姓氏和名字
- 品牌名：商业、工业、办公的商标名
- 狗名

## 格式类

- 地址格式（改为中国的从大到小）
- 姓名格式（改为姓 + 名）
- 公共交通名（从“地铁路线 3”改为“地铁 3 号线”）

## 其他修正

- 修正了一些官方翻译的 bug，如“当前趋势”等

# 翻译流程

对于 `Locales\NamesTranslation\预置翻译` 下的甲、乙、丙、丁等目录，各自独立进行翻译流程。

每个目录下的 csv 文件根据类型调用不同的 subagent：

- **road_district_translator**：`区名.csv`、`城市名.csv`、`巷公路街名.csv`、`桥坝名.csv`
- **creature_name_translator**：`男名.csv`、`女名.csv`、`姓氏.csv`、`狗名.csv`
- **brand_translator**：`品牌名.csv`

注：`格式.csv` 无需翻译，请忽略此文件。