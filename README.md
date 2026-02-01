# Amazon Cognito認証サンプル (.NET 8)

.NET 8とAmazon Cognitoを使用した認証・多要素認証（MFA）のサンプルアプリケーションです。

## 開発環境セットアップ手順

### 1. Amazon Cognito User Poolの設定

#### User Poolの作成

1. AWSコンソールで **Amazon Cognito** を開く
2. **ユーザープールを作成** をクリック

#### サインイン体験の設定

- **Cognitoユーザープールのサインインオプション**: `Eメール` にチェック

#### セキュリティ要件の設定

- **パスワードポリシー**: デフォルト設定
- **多要素認証（MFA）**: `オプションのMFA` または `MFAを必須` を選択
- **MFAメソッド**: `認証アプリ` にチェック（TOTP）

#### サインアップ体験の設定

- **自己登録**: `自己登録を有効にする` にチェック
- **属性検証**: Cognitoが自動的にE メールアドレスを確認
- **必須の属性**: `email` のみ

#### メッセージ配信の設定

- **Eメール**: `Cognito で E メールを送信` を選択（開発/テスト環境向け）

#### アプリケーションの統合

- **ユーザープール名**: 任意（例: `cognito-sample-pool`）
- **アプリケーションタイプ**: `パブリッククライアント`
- **アプリケーションクライアント名**: 任意（例: `cognito-sample-client`）
- **クライアントシークレット**: `クライアントシークレットを生成しない`
- **認証フロー**: `ALLOW_USER_PASSWORD_AUTH`、`ALLOW_REFRESH_TOKEN_AUTH` を有効化

#### 設定値の取得

作成後、以下の値をメモ：

| 項目 | 取得場所 |
|------|----------|
| User Pool ID | ユーザープール概要ページ（例: `ap-northeast-1_XXXXXXXXX`） |
| Client ID | アプリケーションの統合 > アプリクライアント |

---

### 2. AWS CLIの設定

#### AWS CLIのインストール

macOS:
```bash
brew install awscli
```

Windows:
```bash
winget install Amazon.AWSCLI
```

#### IAMユーザーの作成とアクセスキー取得

1. AWSコンソールで **IAM** を開く
2. **ユーザー** > **ユーザーを作成**
3. ユーザー名を入力して次へ
4. **ポリシーを直接アタッチ** で以下のいずれかを選択：
   - `AmazonCognitoPowerUser`（推奨）
   - または下記のカスタムポリシーを作成
5. ユーザー作成後、**セキュリティ認証情報** タブで **アクセスキーを作成**
6. **CLI** を選択してアクセスキーを作成
7. アクセスキーIDとシークレットアクセスキーをメモ

必要最小限のカスタムポリシー:
```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "cognito-idp:SignUp",
        "cognito-idp:ConfirmSignUp",
        "cognito-idp:InitiateAuth",
        "cognito-idp:RespondToAuthChallenge",
        "cognito-idp:AssociateSoftwareToken",
        "cognito-idp:VerifySoftwareToken",
        "cognito-idp:SetUserMFAPreference",
        "cognito-idp:GetUser",
        "cognito-idp:GlobalSignOut"
      ],
      "Resource": "*"
    }
  ]
}
```

#### AWS CLIの設定

```bash
aws configure
```

プロンプトに従って入力：
```
AWS Access Key ID: [取得したアクセスキーID]
AWS Secret Access Key: [取得したシークレットアクセスキー]
Default region name: ap-northeast-1
Default output format: json
```

設定確認：
```bash
aws sts get-caller-identity
```

---

### 3. appsettings.jsonの設定

`CognitoSample/appsettings.json` を編集：

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "AWS": {
    "Cognito": {
      "Region": "ap-northeast-1",
      "UserPoolId": "ap-northeast-1_XXXXXXXXX",
      "ClientId": "xxxxxxxxxxxxxxxxxxxxxxxxxx",
      "ClientSecret": ""
    }
  }
}
```

| 項目 | 説明 |
|------|------|
| Region | User Poolのリージョン |
| UserPoolId | Cognito User Pool ID |
| ClientId | アプリクライアントID |
| ClientSecret | クライアントシークレット（生成した場合のみ） |

---

### 4. アプリケーションの実行

```bash
cd CognitoSample
dotnet restore
dotnet run
```

ブラウザで http://localhost:5000 にアクセス
