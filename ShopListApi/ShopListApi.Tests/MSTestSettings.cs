// テストをメソッドごとに並列で動かす。
// NET 10 の dotnet new mstest と同じ設定
// 今のテストは DB を使わず、共有する状態も無いので、並列にしても結果は変わらない。
[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]
