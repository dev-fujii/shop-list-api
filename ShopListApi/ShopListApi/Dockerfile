# --- 1段目: 作る（SDK 入りのイメージでビルドする） --- #
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /App

# 手元のフォルダーの中身を全部コピーする（bin・obj は .dockerignore で除く）
COPY . ./
# NuGet のパッケージを取ってくる
RUN dotnet restore
# 動かすのに必要なファイルを out にまとめる
RUN dotnet publish -o out

# --- 2段目: 動かす（ASP.NET Core の実行環境だけのイメージ） --- #
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /App
# 1段目の out の中身だけをコピーする（SDK とソースコードは入れない）
COPY --from=build /App/out .
# コンテナを起動したら API を起動する
ENTRYPOINT ["dotnet", "ShopListApi.dll"]
