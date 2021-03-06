DRFront: A Dynamic Reconfiguration Frontend for Xilinx FPGAs
============================================================
Copyright (C) 2022 Naoki FUJIEDA. All rights reserved.

### 概要

FPGAの一部分をユーザが書き換える形でディジタル回路の設計・開発を行うための
ワークフローを支援するツールです．Xilinx社FPGAの動的再構成機能（DFX）を使用
します．現時点では，Digilent社のNexys A7-100Tボードのみを対象としています．

配布パッケージのダウンロード:
<a href="https://aitech.ac.jp/~dslab/nf/DRFront/DRFront_dist_v0_2_1.zip">
ZIP</a> (2.13 MiB)

### 配布パッケージの内容:
- sources/              : プログラムのソースコード
- base.dcp              : ベース設計の Vivado のチェックポイント
- COPYING               : 著作権表記
- DRFront.exe           : DRFront 本体
- README.txt            : このファイル
- Ookii.Dialogs.Wpf.dll : DRFront が使用する Ookii Dialogs のDLLファイル

### 動作環境
Windows 10 以降 + .NET Framework 4.8
（最新の Windows 10 で動作確認済）

DRFront.exe が非ASCII文字（全角文字など）を含む箇所にあると，Vivado が
正しく動作しません．VHDL のソースディレクトリについても同様です．

### 使い方 
1. **DRFront.exe を実行**
  メインのウィンドウが表示されます．
2. **ソースディレクトリを指定**
  Source Dir. の右側の ... ボタンをクリックすると，フォルダの選択の画面に
  移行します．FPGA に書き込みたい回路の VHDL 記述を含むフォルダを指定して
  ください．正しく指定がされていると，その右下の Top Module の欄に，トップ
  モジュールの entity 名が表示されます．
3. **入出力の割当て**
  ボード画像の下に，信号名と方向，割当て先がリストアップされていますので，
  割当て先（Assign to）のコンボボックスの選択を変更するか，信号名を画像上
  にドラッグ＆ドロップすることで，入出力への割当てを行ってください．
  もしソースファイルを変更したことで，入出力の信号名が変わってしまった場合
  には，Refresh ボタンを押して変更を反映させてください．
  また，信号名の順に単に LED（LD）やスイッチ（SW）への割当てをすればいいだ
  けであれば，Auto Assignment ボタンで自動割当ても可能です．
4. **論理合成用の Vivado のプロジェクトを作成**
  Create/Open Project ボタンを押すと，ソースファイル一式を含んだ Vivado の
  プロジェクトを作成します．作成されたプロジェクトを使って Vivado で論理合
  成を行い，チェックポイントファイル（.dcp）をプロジェクトフォルダの直下に
  保存してください．
  なお，作成したプロジェクトを Vivado 上で編集する場合は，左上の Project
  欄で開きたいプロジェクトを選び，Create/Open Project ボタンを押してくださ
  い．プロジェクトのフォルダ内にある xpr ファイルを開いても構いません．
5. **ベースの回路と論理合成した回路を組合せて，配置配線を行う**
  Generate Bitstream ボタンを押すと，あらかじめ用意されたベースの回路と，
  4で作成した回路とをマージし，その配置配線を行うために Vivado が開きます．
  配置配線が完了し，ビットストリームが作成されると，Vivado は自動的に閉じ
  ます．
6. **生成されたビットストリームを FPGA に書き込み**
  Open Hardware Manager ボタンを押すと，Vivado の Hardware Manager が開き
  ます．FPGA と接続し，5で作成されたビットストリームを FPGA に書き込んでく
  ださい．

### 注意事項
トップモジュールやそのポート名は極力自動で認識するようプログラミングして
いますが，以下の前提があります．前提に基づかない記述がある場合，これらが
正しく認識されない場合があります．
- port 文の定義（信号名[, 信号名2...] : 方向 型）は，1つにつき1行で書かれ
  ている必要があります．1つの定義が2行以上に渡る場合，1つの行に2つ以上の
  定義がある場合，信号がうまく認識されません．
- port 文の型は std_logic や固定幅の std_logic_vector であるとします．
- トップモジュールは，他からインスタンス化されていない回路のうち，回路以
  下に含まれるインスタンスの数が最も多いものとします．ただし，入出力のな
  い entity は（テストベンチと思われることから）除外します．

### ライセンス
DRFront には New BSD ライセンスが適用されます．また，DRFront が使用する
Ookii Dialogs は New BSD ライセンスに従って DLL ファイルを再配布していま
す．詳細は COPYING ファイルを参照してください．

### 更新履歴
- v0.2.1 2022-07-13
  - ベース設計のチェックポイントに任意のファイル名をつけられるよう変更．
  - ベース設計のチェックポイントの上書きを防ぐため，Generate Bitstream
    ボタンを押したときに，ベース設計のチェックポイントをプロジェクトに
    コピーする仕様に変更．

- v0.2.0 2022-03-02
  - シミュレーション用のプロジェクト作成に対応．
  - VHDL のテンプレートを作成する機能を追加．

- v0.1.0 2022-03-01
  - 最初のバージョン．