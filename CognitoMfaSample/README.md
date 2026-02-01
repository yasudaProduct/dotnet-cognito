# CognitoMfaSample

Form認証（EntityFramework InMemory）とAmazon Cognito多要素認証（Email-OTP）を組み合わせたサンプルアプリケーションです。

## 概要

- ユーザー管理・パスワード認証はEntityFramework InMemoryで行う
- 多要素認証（MFA）のみAmazon CognitoのEmail-OTPを使用
- Cookie認証によるセッション管理

## 機能

- ユーザー登録（EFとCognito両方にユーザー作成）
- メールアドレス確認（Cognito）
- ユーザーログイン（EFでパスワード認証 → CognitoでMFA）
- メールによるワンタイムパスワード（OTP）認証
- ログイン必須ページへのアクセス制御
- ログアウト

## 技術スタック

- .NET 8
- ASP.NET Core MVC
- EntityFramework Core（InMemory）
- Cookie認証
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
   - **メッセージ配信**: Amazon SESと連携

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
  ├─ EFにユーザー作成（パスワードハッシュ保存）
  └─ Cognitoにユーザー作成（MFA用）
      ↓
メール確認コード入力（Cognito）
      ↓
サインイン
  ├─ EFでパスワード認証
  └─ CognitoでMFA開始 → Email-OTP送信
      ↓
OTP入力
      ↓
Cookie認証発行 → ログイン完了
```

## ファイル構成

```
CognitoMfaSample/
├── Controllers/
│   ├── AuthController.cs       # 認証処理
│   ├── DashboardController.cs  # 認証必須ページ
│   └── HomeController.cs       # トップページ
├── Data/
│   └── ApplicationDbContext.cs # EF Core InMemory設定
├── Filters/
│   └── AuthRequiredAttribute.cs # Cookie認証フィルター
├── Models/
│   ├── ApplicationUser.cs      # ユーザーモデル
│   ├── AuthViewModels.cs       # 認証用ViewModel
│   └── ErrorViewModel.cs
├── Services/
│   ├── IUserService.cs         # ユーザー管理インターフェース
│   ├── UserService.cs          # EFベースのユーザー管理
│   ├── ICognitoMfaService.cs   # MFAインターフェース
│   └── CognitoMfaService.cs    # Cognito MFA処理
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

## CognitoSampleとの違い

| 項目 | CognitoSample | CognitoMfaSample |
|------|---------------|------------------|
| ユーザー管理 | Cognito | EntityFramework InMemory |
| パスワード認証 | Cognito | EntityFramework |
| MFA | Cognito Email-OTP | Cognito Email-OTP |
| セッション管理 | セッション + アクセストークン | Cookie認証 |
| 認証フィルター | セッションベース | Cookie認証ベース |

## 注意事項

- InMemoryデータベースはアプリ再起動でデータが消えます（開発用）
- SESサンドボックスモードでは、送信元・受信先両方のメールアドレスを検証する必要があります
- EFとCognito両方にユーザーを作成するため、サインアップ時にどちらかが失敗すると不整合が発生する可能性があります（本番環境ではトランザクション的な処理を検討してください）
