# CognitoSample

Amazon Cognitoを使用した認証・多要素認証（Email-OTP）のサンプルアプリケーションです。

## 概要

- ユーザー管理・認証をすべてAmazon Cognitoで行う
- 多要素認証はメールによるワンタイムパスワード（Email-OTP）
- セッションベースのトークン管理

## 機能

- ユーザー登録（サインアップ）
- メールアドレス確認
- ユーザーログイン（サインイン）
- メールによるワンタイムパスワード（OTP）認証
- ログイン必須ページへのアクセス制御
- ログアウト

## 技術スタック

- .NET 8
- ASP.NET Core MVC
- Amazon Cognito（AWSSDK.CognitoIdentityProvider）
- Bootstrap 5

## セットアップ

### 1. Amazon Cognito User Poolの設定

1. AWS Consoleで新しいUser Poolを作成
2. 以下の設定を行う：
   - **サインイン識別子**: メールアドレス
   - **必須属性**: email
   - **MFA**: 必須、Email OTPを有効化
   - **自己登録**: 有効
   - **メッセージ配信**: Amazon SESと連携（本番環境の場合はSESサンドボックス解除が必要）

3. App Clientを作成：
   - **認証フロー**: `ALLOW_USER_PASSWORD_AUTH` を有効化
   - **クライアントシークレット**: 生成する

### 2. AWS CLI設定

```bash
aws configure
```

以下の情報を入力：
- AWS Access Key ID
- AWS Secret Access Key
- Default region name（例: ap-northeast-1）

### 3. appsettings.jsonの設定

```json
{
  "AWS": {
    "Cognito": {
      "Region": "ap-northeast-1",
      "UserPoolId": "ap-northeast-1_xxxxxx",
      "ClientId": "xxxxxxxxxxxxxxxxxx",
      "ClientSecret": "xxxxxxxxxxxxxxxxxx"
    }
  }
}
```

### 4. 実行

```bash
dotnet run
```

## 認証フロー

```
サインアップ
  ↓
メール確認コード入力
  ↓
サインイン（メール + パスワード）
  ↓
Email-OTP送信
  ↓
OTP入力
  ↓
ログイン完了
```

## ファイル構成

```
CognitoSample/
├── Controllers/
│   ├── AuthController.cs       # 認証処理
│   ├── DashboardController.cs  # 認証必須ページ
│   └── HomeController.cs       # トップページ
├── Filters/
│   └── AuthRequiredAttribute.cs # セッションベース認証フィルター
├── Models/
│   ├── AuthViewModels.cs       # 認証用ViewModel
│   └── ErrorViewModel.cs
├── Services/
│   └── CognitoService.cs       # Cognito API呼び出し
├── Views/
│   ├── Auth/
│   │   ├── SignUp.cshtml
│   │   ├── ConfirmSignUp.cshtml
│   │   ├── SignIn.cshtml
│   │   ├── EmailOtpChallenge.cshtml
│   │   └── Profile.cshtml
│   ├── Dashboard/
│   │   ├── Index.cshtml
│   │   └── SecretPage.cshtml
│   ├── Home/
│   │   └── Index.cshtml
│   └── Shared/
│       └── _Layout.cshtml
├── Program.cs
└── appsettings.json
```

## 注意事項

- SESサンドボックスモードでは、送信元・受信先両方のメールアドレスを検証する必要があります
- 本番環境ではSESサンドボックスを解除してください
- CognitoとSESのリージョンが一致していることを確認してください
