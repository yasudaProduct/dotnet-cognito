# Amazon Cognito認証サンプル (.NET 8)

このプロジェクトは、.NET 8とAmazon Cognitoを使用した認証・多要素認証（MFA）のサンプルアプリケーションです。

## 機能

- ユーザー登録（サインアップ）
- メールアドレス確認
- ログイン（サインイン）
- 多要素認証（TOTP）
  - 初回ログイン時のMFAセットアップ
  - ログイン後のMFA設定
- ログアウト
- プロフィール表示

## 前提条件

- .NET 8 SDK
- AWSアカウント
- AWS CLI（オプション、設定確認用）

## AWS Cognito User Pool の設定

### 1. User Poolの作成

1. AWSコンソールにログインし、**Amazon Cognito** を開きます
2. **ユーザープールを作成** をクリック

### 2. サインイン体験の設定

1. **Cognitoユーザープールのサインインオプション**
   - `Eメール` にチェック
2. **次へ** をクリック

### 3. セキュリティ要件の設定

1. **パスワードポリシー**
   - パスワードの最小文字数: 8
   - 必要な文字タイプ: 少なくとも1つの数字、特殊文字、大文字、小文字
2. **多要素認証（MFA）**
   - `オプションのMFA` または `MFAを必須` を選択
   - **MFAメソッド**: `認証アプリ` にチェック（TOTP）
3. **次へ** をクリック

### 4. サインアップ体験の設定

1. **自己登録**
   - `自己登録を有効にする` にチェック
2. **属性検証とユーザーアカウントの確認**
   - Cognito が自動的にメッセージを送信して、E メールアドレスを確認
3. **必須の属性**
   - `email` のみ
4. **次へ** をクリック

### 5. メッセージ配信の設定

1. **Eメール**
   - 開発/テスト環境: `Cognito で E メールを送信` を選択
   - 本番環境: Amazon SES を設定することを推奨
2. **次へ** をクリック

### 6. アプリケーションの統合

1. **ユーザープール名**: 任意の名前（例: `cognito-sample-pool`）
2. **アプリケーションクライアント**
   - **アプリケーションタイプ**: `パブリッククライアント`
   - **アプリケーションクライアント名**: 任意（例: `cognito-sample-client`）
   - **クライアントシークレット**: `クライアントシークレットを生成しない`
   - **認証フロー**:
     - `ALLOW_USER_PASSWORD_AUTH` を必ず有効にする
     - `ALLOW_REFRESH_TOKEN_AUTH` を有効にする
3. **次へ** をクリック

### 7. 確認と作成

設定内容を確認し、**ユーザープールを作成** をクリック

### 8. 設定値の取得

作成後、以下の値をメモします：

1. **User Pool ID**
   - ユーザープールの概要ページに表示（例: `ap-northeast-1_XXXXXXXXX`）
2. **Client ID**
   - アプリケーションの統合 > アプリクライアントリスト > 作成したクライアント
   - クライアントIDをコピー

## アプリケーションの設定

`appsettings.json` を編集し、取得した値を設定：

```json
{
  "AWS": {
    "Cognito": {
      "Region": "ap-northeast-1",
      "UserPoolId": "YOUR_USER_POOL_ID",
      "ClientId": "YOUR_CLIENT_ID"
    }
  }
}
```

## AWS認証情報の設定

アプリケーションがAWS Cognitoにアクセスするために、以下のいずれかの方法でAWS認証情報を設定します：

### 方法1: AWS CLIプロファイル（推奨）

```bash
aws configure
```

### 方法2: 環境変数

```bash
export AWS_ACCESS_KEY_ID=your_access_key
export AWS_SECRET_ACCESS_KEY=your_secret_key
export AWS_REGION=ap-northeast-1
```

### 方法3: IAMロール（EC2/ECS/Lambda）

本番環境ではIAMロールの使用を推奨します。

### 必要なIAMポリシー

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
      "Resource": "arn:aws:cognito-idp:ap-northeast-1:YOUR_ACCOUNT_ID:userpool/YOUR_USER_POOL_ID"
    }
  ]
}
```

## 実行方法

```bash
cd CognitoSample
dotnet restore
dotnet run
```

ブラウザで `https://localhost:5001` または `http://localhost:5000` にアクセス

## 使用方法

### 1. アカウント作成

1. 右上の **Sign Up** をクリック
2. メールアドレスとパスワードを入力
3. 確認コードがメールに届くので入力

### 2. ログイン

1. 右上の **Login** をクリック
2. メールアドレスとパスワードを入力

### 3. MFA設定（MFAがオプションの場合）

1. ログイン後、プロフィールページで **MFAを設定** をクリック
2. 認証アプリ（Google Authenticator等）でシークレットキーを登録
3. 表示される6桁のコードを入力

### 4. MFA付きログイン

1. MFA設定後、次回ログイン時に6桁のコードを求められます
2. 認証アプリに表示されるコードを入力

## トラブルシューティング

### "UserPoolId is required" エラー

`appsettings.json` の `UserPoolId` が正しく設定されているか確認してください。

### 認証エラー

- AWS認証情報が正しく設定されているか確認
- IAMユーザー/ロールに必要な権限があるか確認

### MFAコードが無効

- 認証アプリの時刻が正確か確認（NTP同期）
- コードは30秒ごとに更新されるため、素早く入力

### メールが届かない

- Cognitoのメール送信制限を確認
- 迷惑メールフォルダを確認
- 本番環境ではAmazon SESの設定を推奨

## 参考リンク

- [Amazon Cognito ドキュメント](https://docs.aws.amazon.com/cognito/)
- [AWS SDK for .NET](https://docs.aws.amazon.com/sdk-for-net/)
- [AWSSDK.CognitoIdentityProvider NuGet](https://www.nuget.org/packages/AWSSDK.CognitoIdentityProvider/)
