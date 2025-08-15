**Arm64** 環境のみ実行可能です（Apple Silicon で動作確認）。

まずは `libjitshim` をコンパイルします。
```shell
clang -O2 -Wall -Wextra -fPIC -dynamiclib libjitshim.c -o libjitshim.dylib
```

（.NET をインストールして）`dotnet run -c Release` で実行できます。

`Program.cs` の 237 行目で Brainfuck のソースコードを変更可能です。
