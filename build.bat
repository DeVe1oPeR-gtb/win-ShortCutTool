@echo off
chcp 65001 > nul
setlocal

:: プログラム名を定数として定義
set PROGRAM_NAME=ShortcutTool.exe

:: C#コンパイラのパスをセット
set CSC="%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist %CSC% (
    set CSC="%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
)
if not exist %CSC% (
    echo C#コンパイラが見つかりません
    pause
    exit /b
)

:: 既存のexeを削除
if exist %PROGRAM_NAME% (
    del %PROGRAM_NAME%
)

:: C#コードをすべてコンパイル
%CSC% /target:winexe /out:%PROGRAM_NAME% src\Program.cs src\MainForm.cs src\Settings.cs src\ShortcutParser.cs src\Logger.cs

:: ビルド結果を確認
if exist %PROGRAM_NAME% (
    echo ビルド成功！ %PROGRAM_NAME% が生成されました。
) else (
    echo ビルド失敗...
)

pause
chcp 932 > nul
