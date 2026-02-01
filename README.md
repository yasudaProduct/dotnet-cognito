# Amazon Cognito認証サンプル (.NET 8)

.NET 8とAmazon Cognitoを使用した認証・多要素認証（MFA）のサンプルアプリケーション集です。

## サンプルプロジェクト

| プロジェクト | 説明 |
|-------------|------|
| [CognitoSample](./CognitoSample/) | Cognito認証のみを使用したサンプル。ユーザー管理・認証すべてをCognitoで行う |
| [CognitoMfaSample](./CognitoMfaSample/) | Form認証 + Cognito MFAのサンプル。EF InMemoryでユーザー管理、MFAのみCognitoを使用 |

## 共通セットアップ

### 1. Amazon Cognito User Poolの設定

#### User Poolの作成

1. AWSコンソールで **Amazon Cognito** を開く
2. **ユーザープールを作成** をクリック

#### サインイン体験の設定

- **Cognitoユーザープールのサインインオプション**: `Eメール` にチェック

#### セキュリティ要件の設定

- **パスワードポリシー**: デフォルト設定
- **多要素認証（MFA）**: `MFAを必須` を選択
- **MFAメソッド**: `Eメールメッセージ` にチェック（Email-OTP）

#### サインアップ体験の設定

- **自己登録**: `自己登録を有効にする` にチェック
- **属性検証**: Cognitoが自動的にE メールアドレスを確認
- **必須の属性**: `email` のみ

#### メッセージ配信の設定

- **Eメール**: Amazon SESを使用（サンドボックスモードでは送信先メールアドレスの検証が必要）

#### アプリケーションの統合

- **ユーザープール名**: 任意（例: `cognito-sample-pool`）
- **アプリケーションタイプ**: `秘密クライアント`
- **アプリケーションクライアント名**: 任意（例: `cognito-sample-client`）
- **クライアントシークレット**: `クライアントシークレットを生成する`
- **認証フロー**: `ALLOW_USER_PASSWORD_AUTH`、`ALLOW_REFRESH_TOKEN_AUTH` を有効化

#### 設定値の取得

作成後、以下の値をメモ：

| 項目 | 取得場所 |
|------|----------|
| User Pool ID | ユーザープール概要ページ（例: `ap-northeast-1_XXXXXXXXX`） |
| Client ID | アプリケーションの統合 > アプリクライアント |
| Client Secret | アプリケーションの統合 > アプリクライアント > クライアントシークレットを表示 |

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
4. **ポリシーを直接アタッチ** で `AmazonCognitoPowerUser` を選択
5. ユーザー作成後、**セキュリティ認証情報** タブで **アクセスキーを作成**
6. **CLI** を選択してアクセスキーを作成
7. アクセスキーIDとシークレットアクセスキーをメモ

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

各プロジェクトの `appsettings.json` を編集：

```json
{
  "AWS": {
    "Cognito": {
      "Region": "ap-northeast-1",
      "UserPoolId": "ap-northeast-1_XXXXXXXXX",
      "ClientId": "xxxxxxxxxxxxxxxxxxxxxxxxxx",
      "ClientSecret": "xxxxxxxxxxxxxxxxxxxxxxxxxx"
    }
  }
}
```

| 項目 | 説明 |
|------|------|
| Region | User Poolのリージョン |
| UserPoolId | Cognito User Pool ID |
| ClientId | アプリクライアントID |
| ClientSecret | クライアントシークレット |

---

### 4. アプリケーションの実行

```bash
# CognitoSampleの場合
dotnet run --project CognitoSample

# CognitoMfaSampleの場合
dotnet run --project CognitoMfaSample
```

ブラウザで https://localhost:5001 にアクセス

---

## 注意事項

- SESサンドボックスモードでは、送信元・受信先両方のメールアドレスを検証する必要があります
- 本番環境ではSESサンドボックスを解除してください
- CognitoとSESのリージョンが一致していることを確認してください
