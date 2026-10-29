// テストをメソッドごとに並列で動かす。
// NET 10 の dotnet new mstest と同じ設定
// DB を使わないテストは、共有する状態が無い。
// DB 統合テストはサーバーを共有するが、テストごとに専用の DB を作って消すので、並列にしても結果は変わらない。
[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]
