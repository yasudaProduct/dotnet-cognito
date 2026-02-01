# Cognito Email OTP サンプルアプリケーション

このプロジェクトは、**既存の独自パスワード認証を残したまま、Email OTP のみを Amazon Cognito User Pools で実施する**デモアプリケーションです。

## 📋 目的

- ローカルのパスワード認証で第一段階の認証を行う
- Cognito では Email OTP チャレンジのみを実行（二重パスワードを回避）
- アプリのログイン状態は Cookie 認証で保持
- Cognito のトークンは認証確認のみに使用（アプリ内の認証には使用しない）

## 🚀 技術スタック

- .NET 8
- ASP.NET Core MVC
- Amazon Cognito User Pools（AWS SDK for .NET）
- Cookie 認証

## ⚡ クイックスタートガイド

このサンプルを動作させるために必要な主要設定：

### AWS 側の設定（必須）

1. **Cognito User Pool を作成** → 詳細は [セクション 1](#1-aws-cognito-user-pool-の設定)
   - リージョン: 東京（ap-northeast-1）推奨
   - サインインオプション: Email
   - MFA: なし

2. **USER_AUTH 認証フロー を設定** → [セクション 1.11](#111-email-otp-の有効化とパスワード認証の無効化重要)
   - パスワードなしのステータス: **アクティブ**
   - 使用できる選択肢: **Email OTP のみ**（パスワードは無効）

3. **アプリクライアント を設定** → [セクション 1.9](#19-アプリクライアントの認証フロー設定重要)
   - 認証フロー: `ALLOW_USER_AUTH` を有効化

4. **テストユーザー を作成** → [セクション 1.12](#112-テストユーザーの作成重要)
   - メールアドレス: `user@example.com`
   - パスワード: **設定しない**（空欄）
   - メール確認済み: チェック

5. **IAM ユーザー と認証情報** → [セクション 2](#2-iam-ユーザーと認証情報の設定)
   - Cognito へのアクセス権限を持つ IAM ユーザーを作成
   - `aws configure` で認証情報を設定

### アプリ側の設定

6. **appsettings.Development.json を編集** → [セクション 3.1](#31-appsettingsdevelopmentjson-の編集)
   ```json
   {
     "AWS": {
       "Region": "ap-northeast-1",
       "Cognito": {
         "UserPoolId": "ap-northeast-1_XXXXXXXXX",
         "ClientId": "your-client-id",
         "ClientSecret": "your-client-secret"
       }
     }
   }
   ```

7. **アプリを実行**
   ```bash
   dotnet run
   ```

詳細な設定手順は下記の [セットアップ手順](#️-セットアップ手順) を参照してください。

## 🔐 セキュリティ: 多要素認証（MFA/2FA）について

### このサンプルのセキュリティ特性

このサンプルは **2要素認証（2FA/Multi-Factor Authentication）** を実現していますが、**Amazon Cognito の MFA 機能**は使用していません。

#### 認証要素の構成

| 段階 | 認証方法 | 要素の種類 | 詳細 |
|------|---------|-----------|------|
| **第1段階** | ローカル認証 | **知識要素**<br/>(Something you know) | Email + Password<br/>アプリのデータベースで検証 |
| **第2段階** | Cognito Email OTP | **所持要素**<br/>(Something you have) | メールアドレスへのアクセス権<br/>OTP コードの受信・入力 |

**結果**: 知識要素（パスワード） + 所持要素（メールアクセス） = **2要素認証（2FA）** ✅

### Cognito の「MFA」設定について

AWS Cognito には専用の「多要素認証（MFA）」機能がありますが、このサンプルでは使用していません。

#### Cognito の MFA 機能とは

```
Cognito User Pool の MFA 設定:
├─ MFA の強制: 必須 / オプション / なし  ← このサンプル: "なし"
├─ SMS MFA（電話番号に SMS で OTP 送信）
└─ TOTP MFA（Authenticator アプリで OTP 生成）
```

#### EMAIL_OTP は MFA 機能ではない

EMAIL_OTP は Cognito の「MFA 機能」ではなく、**USER_AUTH 認証フローの認証方法の一つ**です：

```
USER_AUTH 認証フロー（パスワードレス認証）:
├─ PASSWORD（パスワード認証）
├─ EMAIL_OTP（Email OTP）          ← このサンプルで使用
├─ SMS_OTP（SMS OTP）
└─ WEB_AUTHN（WebAuthn/FIDO2）
```

### 用語の整理

| 項目 | Cognito の MFA 機能 | このサンプルの実装 |
|------|-------------------|------------------|
| **Cognito の MFA 機能を使用** | ✅ 使用 | ❌ 不使用 |
| **セキュリティ上の 2FA を実現** | ✅ 実現 | ✅ 実現 |
| **認証要素の数** | 2要素 | 2要素 |
| **実装方法** | PASSWORD + SMS/TOTP | PASSWORD（ローカル）+ EMAIL_OTP（Cognito） |
| **AWS コンソールの「MFA」設定** | 必須/オプション | **なし** |

### 前提条件: Cognito にユーザー情報が必要

このサンプルで EMAIL_OTP を使用するには、**Cognito User Pool にユーザー情報が存在している必要があります**。

**必要な情報**:
- ✅ Email アドレス（OTP の送信先）
- ✅ Email 確認済み（`email_verified = true`）
- ⬜ パスワード（不要、設定しない）

**理由**:
- `InitiateAuth` API は `USERNAME` で User Pool からユーザーを検索
- ユーザーが存在しない場合は `UserNotFoundException` が発生
- OTP はユーザーの `email` 属性に送信される

**ユーザー管理の役割分担**:
```
[ローカル側（アプリ）]           [Cognito User Pool]
user@example.com                 user@example.com
password: Password123!           password: (なし)
role, profile, etc.              email_verified: true
        ↓                                ↓
  [認証とユーザー管理]            [OTP 送信インフラ]
  マスター情報として管理          OTP 送信先として利用
```

### まとめ

✅ **このサンプルの特徴**:
- セキュリティ上の多要素認証（2FA）: **実現している**
- Cognito の MFA 機能: **使用していない**
- 既存のパスワード認証と Cognito Email OTP の組み合わせ: **問題なし**
- Cognito にユーザー情報が必要: **はい**（Email と email_verified のみ）

❌ **誤解を避けるべき点**:
- 「Cognito MFA を使っている」← 誤り（MFA 機能は使っていない）
- 「多要素認証ではない」← 誤り（2FA は実現している）
- 「Cognito にユーザー情報は不要」← 誤り（Email OTP の送信先として必要）

## 📁 プロジェクト構成

```
CognitoEmailOtpSample/
├── Controllers/
│   ├── AuthController.cs          # 認証フロー（SignIn / EmailOtpChallenge）
│   └── DashboardController.cs     # ログイン後のダッシュボード
├── Services/
│   ├── ICognitoEmailOtpService.cs # Cognito Email OTP インターフェース
│   ├── CognitoEmailOtpService.cs  # Cognito Email OTP 実装
│   ├── ILocalAuthService.cs       # ローカル認証インターフェース
│   └── LocalAuthService.cs        # ローカル認証実装（インメモリ）
├── Models/
│   ├── SignInViewModel.cs         # サインインフォーム用モデル
│   └── OtpChallengeViewModel.cs   # OTP検証フォーム用モデル
├── Views/
│   ├── Auth/
│   │   ├── SignIn.cshtml          # サインイン画面
│   │   └── EmailOtpChallenge.cshtml # OTP入力画面
│   └── Dashboard/
│       └── Index.cshtml            # ダッシュボード画面
└── appsettings.json                # 設定ファイル
```

## ⚙️ セットアップ手順

### 1. AWS Cognito User Pool の設定

このセクションでは、AWS Cognito User Pool を作成し、Email OTP 認証を設定します。

#### 1.1 リージョンの選択

1. AWS マネジメントコンソールにログイン
2. 右上のリージョンセレクターで **東京（ap-northeast-1）** を選択
   - 他のリージョンでも動作しますが、`appsettings.json` の設定と一致させる必要があります

#### 1.2 User Pool の作成（ステップ 1: サインインエクスペリエンスの設定）

1. Amazon Cognito サービスを開く
2. **「ユーザープールを作成」** をクリック

**ステップ 1: サインインエクスペリエンスを設定**

1. **Cognito ユーザープールのサインインオプション**:
   - ✅ **Eメール** にチェック
   - ⬜ ユーザー名（チェックを外す）
   - ⬜ 電話番号（チェックを外す）

2. **ユーザー名の要件**:
   - デフォルトのまま

3. **次へ** をクリック

#### 1.3 User Pool の作成（ステップ 2: セキュリティ要件を設定）

**ステップ 2: セキュリティ要件を設定**

1. **パスワードポリシー**:
   - **パスワードポリシーモード**: Cognito のデフォルト（推奨）
   - 本サンプルでは Cognito パスワードを使用しないため、任意の設定でOK

2. **多要素認証（MFA）**:
   - **MFA の強制**: **MFA なし** を選択
   - ⚠️ 重要: 「必須」や「オプション」を選択しないでください（Email OTP は MFA とは別の機能です）

3. **ユーザーアカウントの復旧**:
   - ✅ **セルフサービスのアカウントの復旧を有効化 - 推奨**
   - **E メールのみ** を選択

4. **次へ** をクリック

#### 1.4 User Pool の作成（ステップ 3: サインアップエクスペリエンスを設定）

**ステップ 3: サインアップエクスペリエンスを設定**

1. **セルフサービスのサインアップ**:
   - ⬜ **セルフサービスのサインアップを有効化** → **チェックを外す**
   - このサンプルでは管理者が作成したユーザーのみを使用

2. **属性検証とユーザーアカウントの確認**:
   - **Cognito による検証と確認を許可 - 推奨** を選択
   - **E メールアドレスの検証メッセージを送信** にチェック

3. **必須属性**:
   - ✅ **email** にチェック（既にチェックされているはず）
   - 他の属性は不要

4. **カスタム属性**:
   - 追加不要

5. **次へ** をクリック

#### 1.5 User Pool の作成（ステップ 4: メッセージ配信を設定）

**ステップ 4: メッセージ配信を設定**

1. **E メール**:
   - **E メールプロバイダー**: **Cognito で E メールを送信** を選択
   - ⚠️ テスト用途のみ（1日最大50通まで）
   - FROM E メールアドレス: デフォルト（`no-reply@verificationemail.com`）

   **本番環境の場合**:
   - **Amazon SES で E メールを送信** を選択
   - 事前に Amazon SES でドメインまたはメールアドレスを検証する必要があります

2. **SMS**:
   - 設定不要（このサンプルでは SMS を使用しません）

3. **次へ** をクリック

#### 1.6 User Pool の作成（ステップ 5: アプリケーションを統合）

**ステップ 5: アプリケーションを統合**

1. **ユーザープール名**:
   - 任意の名前を入力（例: `email-otp-test-pool`）

2. **Hosted UI**:
   - ⬜ **Cognito Hosted UI を使用** → **チェックを外す**
   - このサンプルでは独自の UI を使用

3. **初期アプリケーションクライアント**:
   - **アプリケーションのタイプ**: **機密クライアント** を選択
   - **アプリケーションクライアント名**: 任意（例: `email-otp-client`）
   - ✅ **クライアントのシークレットを生成** → **チェックを入れる**（推奨）

4. **詳細なアプリケーションクライアントの設定**:
   - **認証フロー**: 後で設定するため、ここではデフォルトのまま
   - その他の設定: デフォルトのまま

5. **次へ** をクリック

#### 1.7 User Pool の作成（ステップ 6: 確認して作成）

**ステップ 6: 確認して作成**

1. 設定内容を確認
2. **ユーザープールを作成** をクリック

#### 1.8 User Pool ID の取得

作成完了後、以下の情報をメモしてください：

1. User Pool 概要ページで **ユーザープール ID** を確認
   - 形式: `ap-northeast-1_XXXXXXXXX`
   - この値を `appsettings.json` の `UserPoolId` に設定します

#### 1.9 アプリクライアントの認証フロー設定（重要）

1. 作成したユーザープール → **「アプリケーションの統合」** タブ
2. 下にスクロールして **「アプリケーションクライアント」** セクション
3. 作成したアプリクライアント（例: `email-otp-client`）をクリック
4. **「編集」** ボタンをクリック
5. **認証フロー** セクションで以下を設定：
   - ✅ **ALLOW_USER_AUTH** ← 必須
   - ✅ **ALLOW_REFRESH_TOKEN_AUTH** ← 推奨
   - ⬜ その他（ALLOW_USER_SRP_AUTH など）→ チェックを外してOK
6. **「変更を保存」** をクリック

#### 1.10 アプリクライアント情報の取得

1. アプリクライアントの詳細画面で以下をメモ：
   - **クライアント ID**: 長い英数字の文字列
   - **クライアントのシークレット**: 「表示」をクリックして確認
2. これらの値を `appsettings.json` に設定します

#### 1.11 Email OTP の有効化とパスワード認証の無効化（重要）

このステップが最も重要です。EMAIL_OTP のみを使用し、パスワード認証を無効化します。

1. ユーザープール → **「サインインエクスペリエンス」** タブ
2. **「MFA」** セクション → **「編集」** をクリック
3. **「MFA の強制」**:
   - **MFA なし** を選択（既に選択されているはず）
4. **「USER_AUTH 認証フロー」** セクション:
   - **「パスワードなしのステータス」**: **アクティブ** を選択
   - **「使用できる選択肢」**:
     - ⬜ **パスワード** → **チェックを外す**（重要！）
     - ✅ **Email OTP** → **チェックを入れる**
     - ⬜ **SMS OTP** → チェックを外す（使用しない場合）
5. **「変更を保存」** をクリック

**重要**:
- パスワードのチェックを外すことで、`SELECT_CHALLENGE` が返されなくなります
- Email OTP のみにすることで、このサンプルの目的である「パスワード不要の Email OTP 認証」が実現されます

#### 1.12 テストユーザーの作成（重要）

1. ユーザープール → 「ユーザー」タブ
2. 「ユーザーを作成」をクリック
3. 以下の設定で作成：
   - **Eメールアドレス**: `user@example.com`（ローカル認証のテストユーザーと同じ）
   - ✅ **「Eメールを確認済みとしてマークする」をチェック**
   - **招待メッセージを送信**: チェックを外す
   - **一時パスワードを生成**: チェックを**外す**（重要！）
   - パスワード欄: **空欄のまま**（パスワードを設定しない）
4. 「ユーザーを作成」

**重要**:
- このサンプルでは Cognito のパスワード認証を使用しないため、ユーザーにパスワードを設定してはいけません
- パスワードを設定すると `Password Challenge is Required` エラーが発生します
- 同様に `test@example.com` ユーザーも作成してください

**注意**: ローカル認証のテストユーザー（`user@example.com` / `test@example.com`）と同じメールアドレスで Cognito ユーザーを作成してください。

### 2. IAM ユーザーと認証情報の設定

このアプリケーションは AWS SDK を使用して Cognito にアクセスするため、適切な権限を持つ IAM ユーザーが必要です。

#### 2.1 IAM ユーザーの作成（初めて AWS を使用する場合）

1. AWS マネジメントコンソール → **IAM** サービス
2. 左メニュー → **ユーザー** → **ユーザーを作成**
3. **ユーザー名**: 任意（例: `cognito-email-otp-app`）
4. **AWS 認証情報タイプを選択**:
   - ✅ **アクセスキー - プログラムによるアクセス**
5. **次へ: アクセス許可** をクリック

#### 2.2 IAM ポリシーの設定

**方法1: 既存のポリシーを使用（簡単）**

1. **既存のポリシーを直接アタッチ** を選択
2. 以下のポリシーを検索してチェック：
   - **AmazonCognitoPowerUser** （推奨）
   - または **CognitoIdentityProviderFullAccess**（より広範な権限）
3. **次へ: タグ** → **次へ: 確認** → **ユーザーを作成**

**方法2: カスタムポリシーを使用（最小権限）**

より厳格なセキュリティが必要な場合は、以下の最小権限ポリシーを作成：

1. **ポリシーを直接アタッチ** の代わりに **インラインポリシーの追加** をクリック
2. JSON タブを選択し、以下を貼り付け：

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "cognito-idp:InitiateAuth",
        "cognito-idp:RespondToAuthChallenge",
        "cognito-idp:AdminGetUser",
        "cognito-idp:DescribeUserPool",
        "cognito-idp:DescribeUserPoolClient"
      ],
      "Resource": [
        "arn:aws:cognito-idp:ap-northeast-1:*:userpool/ap-northeast-1_*"
      ]
    }
  ]
}
```

3. **ポリシーの確認** → ポリシー名を入力（例: `CognitoEmailOtpMinimalAccess`）
4. **ポリシーの作成** をクリック

#### 2.3 アクセスキーの取得

ユーザー作成完了後、以下の情報が表示されます：

- **アクセスキー ID**: 例 `AKIAIOSFODNN7EXAMPLE`
- **シークレットアクセスキー**: 例 `wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY`

⚠️ **重要**: シークレットアクセスキーはこの画面でのみ表示されます。必ず安全な場所に保存してください。

**CSV のダウンロード** ボタンをクリックして認証情報を保存することを推奨します。

#### 2.4 AWS 認証情報の設定

以下のいずれかの方法で AWS 認証情報をアプリケーションに設定します。

**方法1: AWS CLI で設定（推奨）**

1. AWS CLI をインストール（未インストールの場合）
   ```bash
   # macOS
   brew install awscli

   # Windows
   # https://aws.amazon.com/cli/ からインストーラーをダウンロード
   ```

2. 認証情報を設定
   ```bash
   aws configure
   ```

3. プロンプトに従って入力：
   ```
   AWS Access Key ID [None]: AKIAIOSFODNN7EXAMPLE
   AWS Secret Access Key [None]: wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY
   Default region name [None]: ap-northeast-1
   Default output format [None]: json
   ```

**方法2: 環境変数で設定**

```bash
# macOS / Linux
export AWS_ACCESS_KEY_ID=AKIAIOSFODNN7EXAMPLE
export AWS_SECRET_ACCESS_KEY=wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY
export AWS_REGION=ap-northeast-1

# Windows (PowerShell)
$env:AWS_ACCESS_KEY_ID="AKIAIOSFODNN7EXAMPLE"
$env:AWS_SECRET_ACCESS_KEY="wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY"
$env:AWS_REGION="ap-northeast-1"
```

**方法3: AWS 認証情報ファイルを手動で作成**

ファイルを作成：
- macOS/Linux: `~/.aws/credentials`
- Windows: `C:\Users\USERNAME\.aws\credentials`

内容：
```ini
[default]
aws_access_key_id = AKIAIOSFODNN7EXAMPLE
aws_secret_access_key = wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY
```

設定ファイル（`~/.aws/config`）:
```ini
[default]
region = ap-northeast-1
output = json
```

### 3. アプリケーションの設定

#### 3.1 appsettings.Development.json の編集

プロジェクトルートの `appsettings.Development.json` を開き、以下を設定：

```json
{
  "AWS": {
    "Region": "ap-northeast-1",
    "Cognito": {
      "UserPoolId": "ap-northeast-1_XXXXXXXXX",  // ← User Pool ID を入力
      "ClientId": "your-client-id",               // ← アプリクライアント ID を入力
      "ClientSecret": "your-client-secret"        // ← クライアントシークレットを入力（任意）
    }
  }
}
```

**設定値の取得方法**:

これらの値は「1. AWS Cognito User Pool の設定」で取得した値を使用します：

- **Region**: `ap-northeast-1`（User Pool を作成したリージョン）
- **UserPoolId**: セクション 1.8 で取得した User Pool ID（例: `ap-northeast-1_XXXXXXXXX`）
- **ClientId**: セクション 1.10 で取得したクライアント ID
- **ClientSecret**: セクション 1.10 で取得したクライアントのシークレット
  - クライアントシークレットを生成しなかった場合は空文字 `""` を設定

**確認方法**（忘れた場合）:
1. AWS コンソール → Cognito User Pool → 対象の User Pool を選択
2. **UserPoolId**: 「一般設定」タブの「プール ID」
3. **ClientId / ClientSecret**: 「アプリケーションの統合」タブ → 「アプリケーションクライアント」セクション → 対象クライアントをクリック
   - クライアント ID: そのまま表示されています
   - クライアントのシークレット: 「表示」ボタンをクリック

#### 3.2 設定確認チェックリスト

アプリケーションを実行する前に、以下の設定が完了していることを確認してください：

**AWS Cognito 側の設定**:
- ✅ User Pool が作成されている（セクション 1.2～1.7）
- ✅ User Pool ID を取得した（セクション 1.8）
- ✅ アプリクライアントの認証フローで `ALLOW_USER_AUTH` が有効（セクション 1.9）
- ✅ アプリクライアント ID とシークレットを取得した（セクション 1.10）
- ✅ USER_AUTH 認証フローで **Email OTP のみ** が有効、パスワードは無効（セクション 1.11）
- ✅ テストユーザーが作成されている（パスワードなし、メール確認済み）（セクション 1.12）
  - `user@example.com` または `test@example.com`
  - または `yuta.develop.ct@gmail.com`（LocalAuthService.cs に追加したメールアドレス）

**AWS 認証情報の設定**:
- ✅ IAM ユーザーが作成され、Cognito へのアクセス権限がある（セクション 2.1～2.2）
- ✅ AWS 認証情報が設定されている（セクション 2.4）
  - `aws configure` で設定、または
  - 環境変数で設定、または
  - `~/.aws/credentials` ファイルが存在

**アプリケーション側の設定**:
- ✅ `appsettings.Development.json` に正しい値が設定されている（セクション 3.1）
  - Region: `ap-northeast-1`
  - UserPoolId: `ap-northeast-1_XXXXXXXXX`
  - ClientId: 長い英数字の文字列
  - ClientSecret: 長い英数字の文字列（または空文字）

**動作確認コマンド**（オプション）:

AWS CLI で設定を確認：
```bash
# 認証情報が正しく設定されているか確認
aws sts get-caller-identity

# User Pool の情報を取得
aws cognito-idp describe-user-pool --user-pool-id ap-northeast-1_XXXXXXXXX

# アプリクライアントの情報を取得
aws cognito-idp describe-user-pool-client \
  --user-pool-id ap-northeast-1_XXXXXXXXX \
  --client-id your-client-id
```

### 4. アプリケーションの実行

```bash
cd CognitoEmailOtpSample
dotnet restore
dotnet build
dotnet run
```

ブラウザで `https://localhost:5001` （または表示されたURL）にアクセス。

## 🔐 認証フロー

### シーケンス図

```mermaid
sequenceDiagram
    actor User as ユーザー
    participant Browser as ブラウザ
    participant App as ASPNETアプリ
    participant LocalAuth as LocalAuthService
    participant Cognito as Amazon Cognito
    participant Email as メールサービス

    User->>Browser: 1. サインイン画面を開く
    Browser->>App: GET /Auth/SignIn
    App->>Browser: サインインフォームを表示

    User->>Browser: 2. Email + Password を入力
    Browser->>App: POST /Auth/SignIn<br/>(email, password)

    App->>LocalAuth: 3. ローカル認証を実行
    LocalAuth->>LocalAuth: パスワード検証
    LocalAuth-->>App: 認証成功

    App->>Cognito: 4. InitiateAuth<br/>AuthFlow=USER_AUTH<br/>PREFERRED_CHALLENGE=EMAIL_OTP
    Cognito->>Cognito: セッション生成

    alt 複数認証方法が有効な場合
        Cognito-->>App: ChallengeName=SELECT_CHALLENGE
        App->>App: EMAIL_OTP を選択
        App->>Cognito: RespondToAuthChallenge<br/>ANSWER=EMAIL_OTP
        Cognito->>Email: OTPコードをメール送信
        Cognito-->>App: ChallengeName=EMAIL_OTP<br/>Session=xxx
    else EMAIL_OTPのみ有効な場合
        Cognito->>Email: OTPコードをメール送信
        Cognito-->>App: ChallengeName=EMAIL_OTP<br/>Session=xxx
    end

    App->>Browser: OTP入力画面へリダイレクト
    Browser->>App: GET /Auth/EmailOtpChallenge
    App->>Browser: OTP入力フォームを表示

    Email-->>User: 5. OTPコード受信
    User->>Browser: 6. OTPコード(6桁)を入力
    Browser->>App: POST /Auth/EmailOtpChallenge<br/>(session, code)

    App->>Cognito: 7. RespondToAuthChallenge<br/>ChallengeName=EMAIL_OTP<br/>EMAIL_OTP_CODE=xxx
    Cognito->>Cognito: OTPコード検証
    Cognito-->>App: AuthenticationResult<br/>(IdToken, AccessToken)

    App->>App: 8. Cookie認証でサインイン<br/>ClaimsPrincipal作成
    App->>Browser: ダッシュボードへリダイレクト
    Browser->>App: GET /Dashboard/Index
    App->>Browser: ダッシュボード画面を表示

    Browser->>User: 9. 認証完了
```

### フロー概要

```
[ユーザー]
    ↓
[1] メールアドレス + パスワード入力 (/Auth/SignIn)
    ↓
[2] ローカル認証（LocalAuthService）
    ↓ 成功
[3] Cognito Email OTP チャレンジ開始（InitiateAuth）
    - AuthFlow = USER_AUTH
    - PREFERRED_CHALLENGE = EMAIL_OTP
    ↓
[3-1] SELECT_CHALLENGE が返された場合（複数認証方法が有効な場合）
    - RespondToAuthChallenge で EMAIL_OTP を選択
    ↓
[4] メールで OTP コード受信
    ↓
[5] OTP コード入力 (/Auth/EmailOtpChallenge)
    ↓
[6] OTP 検証（RespondToAuthChallenge）
    - ChallengeName = EMAIL_OTP
    ↓ 成功
[7] Cookie 認証でサインイン
    ↓
[8] ダッシュボード表示 (/Dashboard/Index)
```

**注**: USER_AUTH フローで複数の認証方法（EMAIL_OTP、SMS_OTP、PASSWORD など）が有効になっている場合、Cognito は `SELECT_CHALLENGE` を返します。このサンプルでは自動的に `EMAIL_OTP` を選択するよう実装されています。

## 🧪 動作確認手順

### 手順1: サインイン画面でローカル認証

1. アプリケーションを起動
2. サインイン画面が表示される
3. 以下のテストユーザーでサインイン：
   - **Email**: `user@example.com`
   - **Password**: `Password123!`

または

   - **Email**: `test@example.com`
   - **Password**: `Test123!`

4. 「次へ（OTP送信）」をクリック

### 手順2: Email OTP 検証

1. OTP入力画面に遷移
2. 指定したメールアドレスに Cognito から OTP コードが送信される
3. メールを確認し、6桁のコードを入力
4. 「検証」をクリック

### 手順3: ダッシュボード表示

1. OTP 検証が成功すると、ダッシュボードにリダイレクト
2. 認証完了メッセージが表示される
3. ユーザーのメールアドレスが表示される

## ⚠️ 想定されるエラーと対処法

### エラー1: `UserNotFoundException`

**メッセージ**: "ユーザーが見つかりません。Cognito にユーザーが存在することを確認してください。"

**原因**:
- Cognito User Pool にユーザーが存在しない
- メールアドレスのスペルミス

**対処法**:
1. AWS コンソールで User Pool の「ユーザー」タブを確認
2. ローカル認証で使用しているメールアドレスと同じユーザーを作成
3. 「Eメールを確認済みとしてマークする」をチェック

---

### エラー2: `NotAuthorizedException`

**メッセージ**: "認証が拒否されました。App Client の設定を確認してください。"

**原因**:
- `ALLOW_USER_AUTH` 認証フローが有効化されていない
- Client ID または Client Secret が間違っている
- Email OTP が有効化されていない

**対処法**:
1. User Pool → 「アプリケーションの統合」→ アプリクライアント → 「編集」
2. 認証フローで `ALLOW_USER_AUTH` をチェック
3. User Pool → 「サインインエクスペリエンス」→ 「MFA」→ 「USER_AUTH 認証フロー」で `Email OTP` を有効化
4. `appsettings.Development.json` の ClientId / ClientSecret を確認

---

### エラー3: `SELECT_CHALLENGE` が返される

**動作**:
- InitiateAuth を呼び出すと `SELECT_CHALLENGE` が返される
- このサンプルでは自動的に `EMAIL_OTP` を選択して処理を続行

**原因**:
- USER_AUTH フローで複数の認証方法が有効になっている
  - 例: EMAIL_OTP と SMS_OTP の両方が有効
  - 例: EMAIL_OTP と PASSWORD の両方が有効

**対処法**:
このサンプルコードでは自動的に処理されるため、特に対処は不要です。ログに以下のメッセージが表示されます：
```
SELECT_CHALLENGE を受信、EMAIL_OTP を選択します
```

**注**: もし EMAIL_OTP のみを使用したい場合は、Cognito User Pool の設定で他の認証方法（パスワード等）を無効化してください：
1. User Pool → 「サインインエクスペリエンス」→ 「MFA」
2. 「USER_AUTH 認証フロー」で「パスワード」のチェックを外す
3. 「Email OTP」のみにチェックを入れる

---

### 補足: Email OTP コードの桁数について

**OTP コードが8桁の場合**:
- Cognito の Email OTP は通常6桁ですが、環境によっては8桁になる場合があります
- このサンプルコードは6桁～8桁の両方に対応しています
- 桁数の変更は Cognito 側の設定では直接変更できません

**6桁に統一したい場合**:
- User Pool → 「サインインエクスペリエンス」→ 「MFA」
- 「USER_AUTH 認証フロー」で **Email OTP のみ** を有効化
- 他の認証方法（Password、SMS OTP など）を無効化

---

### エラー4: `CodeMismatchException`

**メッセージ**: "OTP コードが正しくありません"

**原因**:
- 入力した OTP コードが間違っている
- OTP コードの有効期限が切れている（通常3分）

**対処法**:
1. メールを再確認し、正しいコードを入力（6桁または8桁）
2. 有効期限が切れた場合は、最初からサインインし直す
3. コードの桁数が合っているか確認（環境によって6桁または8桁）

---

### エラー5: `InvalidParameterException` (SECRET_HASH)

**メッセージ**: "Unable to verify secret hash for client"

**原因**:
- アプリクライアントにクライアントシークレットがあるが、SECRET_HASH を送信していない
- SECRET_HASH の計算が間違っている

**対処法**:
1. `appsettings.Development.json` の `ClientSecret` を正しく設定
2. クライアントシークレットを使わない場合は、AWS コンソールでシークレットなしのクライアントを作成

---

### エラー6: メールが届かない

**原因**:
- Cognito のメール送信制限（Sandbox 環境では1日200通まで）
- メールアドレスが未検証
- スパムフォルダに振り分けられている

**対処法**:
1. スパムフォルダを確認
2. AWS コンソールで User Pool の「ユーザー」タブ → 対象ユーザー → 「Eメールを確認済みとしてマークする」
3. 本番環境では Amazon SES を設定

---

### エラー7: `InvalidParameterException` - "Password Challenge is Required to SignIn"

**メッセージ**: "Cognito ユーザーにパスワードが設定されているか、ユーザーのステータスが FORCE_CHANGE_PASSWORD になっています。"

**原因**:
このエラーは、USER_AUTH フローで EMAIL_OTP を選択しようとしたときに発生します：
- Cognito ユーザーのステータスが `FORCE_CHANGE_PASSWORD`（初回ログイン時のパスワード変更が必要）
- Cognito ユーザーにパスワードが設定されており、PASSWORD 認証が優先される
- ユーザー作成時に一時パスワードを設定したが、まだ変更されていない

**対処法（以下のいずれか）**:

#### 対処法 1: ユーザーを削除して再作成（推奨）

1. AWS コンソール → Cognito User Pool → 「ユーザー」タブ
2. 対象ユーザーを選択して「削除」
3. 「ユーザーを作成」をクリック
4. 以下の設定で作成：
   - **Eメールアドレス**: `user@example.com`
   - ✅ **「Eメールを確認済みとしてマークする」をチェック**
   - **招待メッセージを送信**: チェックを外す
   - **一時パスワードを生成**: チェックを**外す**（重要）
   - パスワード欄は**空欄のまま**
5. 「ユーザーを作成」

#### 対処法 2: AWS CLI でユーザーのステータスを変更

```bash
# ユーザーのステータスを確認
aws cognito-idp admin-get-user \
  --user-pool-id ap-northeast-1_XXXXXXXXX \
  --username user@example.com

# パスワードを設定してステータスを CONFIRMED に変更
aws cognito-idp admin-set-user-password \
  --user-pool-id ap-northeast-1_XXXXXXXXX \
  --username user@example.com \
  --password "TemporaryPassword123!" \
  --permanent
```

**注**: ただし、この方法では Cognito にパスワードが設定されるため、PASSWORD チャレンジが優先される可能性があります。

#### 対処法 3: User Pool の設定を変更

1. User Pool → 「サインインエクスペリエンス」→ 「認証フロー」
2. **USER_AUTH 認証フロー** で以下を確認：
   - ✅ `Email OTP` が有効
   - ⬜ `Password` を無効化（可能であれば）

**重要**: このサンプルの目的は「Cognito でパスワード認証を行わず、Email OTP のみを使用する」ことです。そのため、Cognito ユーザーにはパスワードを設定せず、ステータスを `CONFIRMED` にする必要があります。

---

### エラー8: `AmazonCognitoIdentityProviderException`

**メッセージ**: "Cognito エラー: ..."

**原因**:
- AWS 認証情報が設定されていない
- IAM 権限が不足している
- リージョンが間違っている

**対処法**:
1. AWS CLI で認証情報を設定: `aws configure`
2. IAM ユーザーに `AmazonCognitoReadOnly` 以上の権限を付与
3. `appsettings.json` の `Region` を確認（User Pool と同じリージョンに設定）

## 📝 カスタマイズ方法

### ローカル認証をデータベースに変更

`Services/LocalAuthService.cs` のインメモリ実装を、Entity Framework Core などを使ったデータベース実装に変更：

```csharp
public class LocalAuthService : ILocalAuthService
{
    private readonly ApplicationDbContext _context;

    public LocalAuthService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> ValidateCredentialsAsync(string email, string password)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == email);

        if (user == null) return false;

        // パスワードハッシュの検証（例: BCrypt, PBKDF2）
        return VerifyPassword(password, user.PasswordHash);
    }
}
```

### Cognito トークンをアプリ内で使用

OTP 検証成功後に取得できる Cognito トークンをアプリ内の認証に使用する場合：

`Controllers/AuthController.cs` の `EmailOtpChallenge` メソッドを変更：

```csharp
// OTP 検証時にトークンを取得
var (isValid, tokens) = await _cognitoService.VerifyEmailOtpAsync(...);

// トークンを Cookie や Session に保存
HttpContext.Session.SetString("IdToken", tokens.IdToken);
HttpContext.Session.SetString("AccessToken", tokens.AccessToken);
HttpContext.Session.SetString("RefreshToken", tokens.RefreshToken);
```

## 🔧 トラブルシューティング

### ログの確認

アプリケーションのログレベルを `Debug` に変更して詳細なログを確認：

`appsettings.Development.json`:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "CognitoEmailOtpSample": "Debug"
    }
  }
}
```

### AWS SDK のデバッグ

AWS SDK のログを有効化：

```csharp
// Program.cs
AWSConfigs.LoggingConfig.LogTo = LoggingOptions.Console;
AWSConfigs.LoggingConfig.LogResponses = ResponseLoggingOption.Always;
```

## 📚 参考資料

- [Amazon Cognito User Pools - USER_AUTH フロー](https://docs.aws.amazon.com/cognito/latest/developerguide/amazon-cognito-user-pools-authentication-flow.html#user-auth-flow)
- [AWS SDK for .NET - Cognito Identity Provider](https://docs.aws.amazon.com/sdkfornet/v3/apidocs/items/CognitoIdentityProvider/NCognitoIdentityProvider.html)
- [ASP.NET Core Cookie Authentication](https://learn.microsoft.com/ja-jp/aspnet/core/security/authentication/cookie)

## 📄 ライセンス

このサンプルコードは MIT ライセンスの下で提供されています。
