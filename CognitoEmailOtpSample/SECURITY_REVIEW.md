# CognitoEmailOtpSample セキュリティレビュー

**レビュー実施日**: 2026年2月2日
**対象バージョン**: CUSTOM_AUTH ブランチ
**総合評価**: ⚠️ サンプル/プロトタイプとしては適切、本番利用には重大な修正が必要

---

## 目次

1. [エグゼクティブサマリー](#1-エグゼクティブサマリー)
2. [認証フロー分析](#2-認証フロー分析)
3. [脆弱性と問題点](#3-脆弱性と問題点)
4. [良い実装](#4-良い実装)
5. [推奨対応](#5-推奨対応)
6. [詳細分析](#6-詳細分析)

---

## 1. エグゼクティブサマリー

### 評価概要

| カテゴリ | 評価 | 状態 |
|----------|------|------|
| 認証情報管理 | 🔴 Critical | 要即時対応 |
| Session 管理 | 🟠 High | 要早期対応 |
| 入力検証 | 🟢 Good | 適切 |
| CSRF 保護 | 🟢 Good | 適切 |
| 通信セキュリティ | 🟢 Good | HTTPS 強制 |
| Rate Limiting | 🟠 High | 未実装 |

### 主要な発見事項

1. **🔴 Critical**: Cognito Client Secret が `appsettings.Development.json` にハードコード
2. **🔴 Critical**: パスワードが平文でソースコードにハードコード
3. **🔴 Critical**: 実際のメールアドレスがソースコードに含まれている
4. **🟠 High**: Cognito Session トークンが URL パラメータに露出
5. **🟠 High**: OTP 試行回数の制限がない（Rate Limiting 未実装）
6. **🟡 Medium**: Cognito が返す JWT トークンを検証・使用していない

---

## 2. 認証フロー分析

### 2.1 認証フロー図

```
┌─────────────────────────────────────────────────────────────────────┐
│                        認証フロー全体像                              │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  ユーザー          アプリ              LocalAuth        Cognito     │
│     │                │                    │               │         │
│     │ ① Email/PW    │                    │               │         │
│     │───────────────>│                    │               │         │
│     │                │ ② 検証依頼         │               │         │
│     │                │───────────────────>│               │         │
│     │                │    平文比較 ⚠️      │               │         │
│     │                │<───────────────────│               │         │
│     │                │                    │               │         │
│     │                │ ③ InitiateAuth (EMAIL_OTP)        │         │
│     │                │───────────────────────────────────>│         │
│     │                │                    Session         │         │
│     │                │<───────────────────────────────────│         │
│     │                │                                    │         │
│     │ ④ Redirect     │                                    │         │
│     │  (URL に Session 露出) ⚠️                           │         │
│     │<───────────────│                                    │         │
│     │                │                                    │         │
│     │ ⑤ OTP入力      │                                    │         │
│     │───────────────>│                                    │         │
│     │                │ ⑥ RespondToAuthChallenge           │         │
│     │                │───────────────────────────────────>│         │
│     │                │    IdToken/AccessToken (未使用) ⚠️ │         │
│     │                │<───────────────────────────────────│         │
│     │                │                                    │         │
│     │ ⑦ Cookie発行   │                                    │         │
│     │<───────────────│                                    │         │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

### 2.2 フェーズ別分析

#### Phase 1: ローカルパスワード認証

**該当ファイル**: `Services/LocalAuthService.cs`

```csharp
// Line 32-38
if (_users.TryGetValue(email, out var storedPassword))
{
    if (storedPassword == password)  // ⚠️ 平文比較
    {
        return Task.FromResult(true);
    }
}
```

**問題点**:
- パスワードが平文で保存・比較されている
- タイミング攻撃（Timing Attack）に脆弱
- ハードコードされたテストユーザー情報

---

#### Phase 2: Cognito OTP チャレンジ開始

**該当ファイル**: `Controllers/AuthController.cs`

```csharp
// Line 60-64
return RedirectToAction(nameof(EmailOtpChallenge), new
{
    email = model.Email,
    session = session  // ⚠️ URL パラメータに露出
});
```

**結果の URL**:
```
/Auth/EmailOtpChallenge?email=user@example.com&session=AYADeJl2AABCAyPR...
```

**問題点**:
- Session トークンがブラウザ履歴に残る
- サーバーのアクセスログに記録される
- Referer ヘッダー経由で外部サイトに漏洩する可能性

---

#### Phase 3: OTP 検証

**該当ファイル**: `Services/CognitoEmailOtpService.cs`

```csharp
// Line 176-181
if (response.AuthenticationResult != null)
{
    _logger.LogInformation("Email OTP 検証成功");
    _logger.LogDebug("IdToken: {IdToken}", ...);   // ログに記録
    _logger.LogDebug("AccessToken: {AccessToken}", ...);  // 未使用
    return true;  // トークンを破棄
}
```

**問題点**:
- Cognito が発行した JWT トークンを検証していない
- トークンの署名検証をスキップ
- Cognito が保証するユーザー情報（sub, email_verified 等）を活用していない

---

#### Phase 4: Cookie 認証発行

**該当ファイル**: `Controllers/AuthController.cs`

```csharp
// Line 123-137
var claims = new List<Claim>
{
    new Claim(ClaimTypes.Name, model.Email),
    new Claim(ClaimTypes.Email, model.Email)
};

var authProperties = new AuthenticationProperties
{
    IsPersistent = true,  // 常に永続化
    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(1)
};
```

**問題点**:
- Cognito ユーザー ID（sub）を Claims に含めていない
- `IsPersistent = true` が固定（ユーザー選択なし）
- Cookie のセキュリティ属性が明示的に設定されていない

---

## 3. 脆弱性と問題点

### 3.1 🔴 Critical（緊急対応必要）

#### C-1: 秘密情報のハードコード

**ファイル**: `appsettings.Development.json`

```json
{
  "AWS": {
    "Cognito": {
      "UserPoolId": "ap-northeast-1_EEuGsaDHX",
      "ClientId": "5557ecqv9esfh5snsipn1kcaut",
      "ClientSecret": "1m7oouhcrg3k65luq4aehik2m6ecroio0ofchsi311se091m923r"
    }
  }
}
```

**リスク**:
- ソースコードがリポジトリにコミットされると、認証情報が公開される
- 攻撃者が Cognito API を直接呼び出し可能になる
- 他のユーザーアカウントへのアクセス試行が可能

**CVSS スコア**: 9.1 (Critical)

---

#### C-2: パスワードの平文保存

**ファイル**: `Services/LocalAuthService.cs`

```csharp
private readonly Dictionary<string, string> _users = new()
{
    { "user@example.com", "Password123!" },
    { "test@example.com", "Test123!" },
    { "yuta.develop.ct@gmail.com", "Test123!" }
};
```

**リスク**:
- コンパイル済みバイナリからパスワードを抽出可能
- リバースエンジニアリングでアプリケーション全体が危険にさらされる
- 実際のメールアドレス（`yuta.develop.ct@gmail.com`）が公開されている

**CVSS スコア**: 8.6 (High)

---

#### C-3: 個人情報の露出

**影響箇所**:
- `LocalAuthService.cs:16` - メールアドレスのハードコード
- ソースコード内のコメント

**リスク**:
- 個人のメールアドレスが特定される
- フィッシング攻撃やスパムの対象になる可能性
- Git 履歴に永続的に残る

---

### 3.2 🟠 High（早期対応推奨）

#### H-1: Session トークンの URL 露出

**ファイル**: `Controllers/AuthController.cs:60-64`

**漏洩経路**:

| 経路 | 影響度 | 説明 |
|------|--------|------|
| ブラウザ履歴 | 高 | 共有端末で Session が見える |
| サーバーログ | 中 | アクセスログに Session が記録 |
| Referer ヘッダー | 高 | 外部リンクで漏洩 |
| ショルダーハック | 低 | 画面を見られると露出 |

**推奨修正**:
```csharp
// 修正案: サーバーサイド Session に保存
HttpContext.Session.SetString("AuthSession", session);
HttpContext.Session.SetString("AuthEmail", email);
return RedirectToAction(nameof(EmailOtpChallenge));
```

---

#### H-2: Rate Limiting の欠如

**影響**: OTP 検証エンドポイント全体

**リスク**:
- 6桁 OTP の場合、最大 100万通りの組み合わせ
- ブルートフォース攻撃に対する保護がない
- Cognito 側の Rate Limiting に依存

**推奨対応**:
- `AspNetCoreRateLimit` パッケージの導入
- IP アドレスベースの制限
- アカウントベースの試行回数制限

---

#### H-3: パスワード比較のタイミング攻撃脆弱性

**ファイル**: `Services/LocalAuthService.cs:34`

```csharp
if (storedPassword == password)  // タイミング攻撃に脆弱
```

**問題**:
- 文字列比較は一致しない最初の文字で即座に `false` を返す
- 攻撃者がレスポンス時間の差を計測してパスワードを推測可能

**推奨修正**:
```csharp
// 固定時間比較を使用
using System.Security.Cryptography;

var storedBytes = Encoding.UTF8.GetBytes(storedPassword);
var inputBytes = Encoding.UTF8.GetBytes(password);
return CryptographicOperations.FixedTimeEquals(storedBytes, inputBytes);
```

---

### 3.3 🟡 Medium（改善推奨）

#### M-1: Cognito トークンの未使用

**ファイル**: `Services/CognitoEmailOtpService.cs:176-181`

**問題**:
- Cognito が発行した IdToken / AccessToken を検証・使用していない
- JWT の署名検証がない
- Cognito が保証するユーザー情報を捨てている

**推奨対応**:
```csharp
// JWT 検証の追加
var tokenHandler = new JwtSecurityTokenHandler();
var validationParameters = new TokenValidationParameters
{
    ValidateIssuerSigningKey = true,
    IssuerSigningKeys = await GetCognitoPublicKeysAsync(),
    ValidateIssuer = true,
    ValidIssuer = $"https://cognito-idp.{region}.amazonaws.com/{userPoolId}",
    ValidateAudience = true,
    ValidAudience = clientId,
    ValidateLifetime = true
};

tokenHandler.ValidateToken(idToken, validationParameters, out _);
```

---

#### M-2: Cookie セキュリティ設定の不足

**ファイル**: `Program.cs:11-18`

**現在の設定**:
```csharp
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/SignIn";
        options.LogoutPath = "/Auth/Logout";
        options.ExpireTimeSpan = TimeSpan.FromHours(1);
        options.SlidingExpiration = true;
        // セキュリティ属性が明示的に設定されていない
    });
```

**推奨設定**:
```csharp
options.Cookie.SameSite = SameSiteMode.Strict;
options.Cookie.HttpOnly = true;
options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
options.Cookie.IsEssential = true;
```

---

#### M-3: エラーメッセージの情報漏洩

**ファイル**: `Services/CognitoEmailOtpService.cs:129-138`

```csharp
throw new InvalidOperationException(
    "Cognito ユーザーにパスワードが設定されているか、ユーザーのステータスが FORCE_CHANGE_PASSWORD になっています。..."
);
```

**問題**: Cognito 側のシステム状態を詳細に返しており、ユーザー列挙攻撃を助長する可能性

**推奨**: より汎用的なエラーメッセージに変更

---

#### M-4: 二重ユーザー管理

**設計上の問題**:
```
ローカル認証: Dictionary<email, password> で管理
Cognito:      User Pool で別途管理
```

**リスク**:
- ユーザーの不整合（ローカルに存在するが Cognito に存在しない、またはその逆）
- パスワード変更時の同期問題
- アカウントロック状態の不整合

---

### 3.4 🔵 Low（参考情報）

#### L-1: ログ出力の過剰性

**ファイル**: `Services/CognitoEmailOtpService.cs:179-180`

```csharp
_logger.LogDebug("IdToken: {IdToken}", response.AuthenticationResult.IdToken?.Substring(0, 20) + "...");
```

**問題**: JWT トークンの一部がログに記録される

---

#### L-2: Claims の最小性

**ファイル**: `Controllers/AuthController.cs:123-127`

```csharp
var claims = new List<Claim>
{
    new Claim(ClaimTypes.Name, model.Email),
    new Claim(ClaimTypes.Email, model.Email)
};
```

**不足している情報**:
- `sub`（Cognito ユーザー ID）
- `auth_time`（認証時刻）
- `email_verified`（メール検証済みフラグ）

---

## 4. 良い実装

### 4.1 CSRF 保護 ✅

すべての POST エンドポイントに `[ValidateAntiForgeryToken]` が実装されています。

```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> SignIn(SignInViewModel model)
```

---

### 4.2 HTTPS 強制 ✅

`Program.cs` で HTTPS リダイレクションが有効化されています。

```csharp
app.UseHttpsRedirection();
```

---

### 4.3 SECRET_HASH 計算 ✅

HMAC-SHA256 を使用した適切な SECRET_HASH 計算が実装されています。

```csharp
using var hmac = new HMACSHA256(key);
var hash = hmac.ComputeHash(message);
return Convert.ToBase64String(hash);
```

---

### 4.4 入力バリデーション ✅

ViewModel に適切なバリデーション属性が設定されています。

```csharp
[Required(ErrorMessage = "メールアドレスを入力してください")]
[EmailAddress(ErrorMessage = "有効なメールアドレスを入力してください")]
public string Email { get; set; }

[StringLength(8, MinimumLength = 6, ErrorMessage = "...")]
[RegularExpression(@"^\d{6,8}$", ErrorMessage = "...")]
public string OtpCode { get; set; }
```

---

### 4.5 汎用エラーメッセージ ✅

ユーザー列挙攻撃を防ぐ汎用メッセージが使用されています。

```csharp
ModelState.AddModelError(string.Empty, "メールアドレスまたはパスワードが正しくありません");
```

---

### 4.6 例外ハンドリング ✅

Cognito 例外が適切に分類・処理されています。

```csharp
catch (UserNotFoundException ex) { ... }
catch (NotAuthorizedException ex) { ... }
catch (CodeMismatchException) { ... }
catch (ExpiredCodeException ex) { ... }
```

---

## 5. 推奨対応

### 5.1 優先度別対応表

| 優先度 | 問題 | 対応内容 | 期限目安 |
|--------|------|----------|----------|
| 🔴 P0 | C-1 | Client Secret を Secrets Manager に移行、既存キーをローテーション | 即時 |
| 🔴 P0 | C-2 | ハードコードされたパスワードを削除 | 即時 |
| 🔴 P0 | C-3 | 個人メールアドレスを削除、Git 履歴から削除 | 即時 |
| 🔴 P0 | - | `.gitignore` に `appsettings.*.json` を追加 | 即時 |
| 🟠 P1 | H-1 | Session トークンをサーバーサイド Session に保存 | 1週間 |
| 🟠 P1 | H-2 | Rate Limiting の実装 | 1週間 |
| 🟠 P1 | H-3 | パスワード比較を固定時間比較に変更 | 1週間 |
| 🟡 P2 | M-1 | JWT トークン検証の追加 | 2週間 |
| 🟡 P2 | M-2 | Cookie セキュリティ設定の強化 | 2週間 |
| 🟡 P2 | M-3 | エラーメッセージの改善 | 2週間 |
| 🟡 P2 | M-4 | ユーザー管理の統合検討 | 1ヶ月 |

---

### 5.2 即時対応（24時間以内）

#### 1. `.gitignore` の更新

```gitignore
# Secrets - 追加
appsettings.Development.json
appsettings.Production.json
appsettings.*.json
!appsettings.json
```

#### 2. Client Secret のローテーション

1. AWS コンソールで新しいアプリクライアントを作成
2. 新しい Client ID / Client Secret を取得
3. 環境変数または AWS Secrets Manager に設定
4. 古いアプリクライアントを削除

#### 3. ハードコードされた情報の削除

`LocalAuthService.cs` から実際のメールアドレスとパスワードを削除：

```csharp
// 開発環境のみ: 環境変数から読み込む
private readonly Dictionary<string, string> _users;

public LocalAuthService(IConfiguration configuration)
{
    var testUsers = configuration.GetSection("TestUsers").Get<Dictionary<string, string>>();
    _users = testUsers ?? new Dictionary<string, string>();
}
```

---

### 5.3 短期対応（1週間以内）

#### Session トークンの修正

```csharp
// AuthController.cs - SignIn POST
HttpContext.Session.SetString("AuthSession", session);
HttpContext.Session.SetString("AuthEmail", model.Email);
return RedirectToAction(nameof(EmailOtpChallenge));

// AuthController.cs - EmailOtpChallenge GET
[HttpGet]
public IActionResult EmailOtpChallenge()
{
    var email = HttpContext.Session.GetString("AuthEmail");
    var session = HttpContext.Session.GetString("AuthSession");

    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(session))
    {
        return RedirectToAction(nameof(SignIn));
    }

    var model = new OtpChallengeViewModel
    {
        Email = email,
        Session = session
    };

    return View(model);
}
```

#### Rate Limiting の追加

```csharp
// Program.cs
builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(options =>
{
    options.GeneralRules = new List<RateLimitRule>
    {
        new RateLimitRule
        {
            Endpoint = "POST:/Auth/EmailOtpChallenge",
            Period = "1m",
            Limit = 5
        }
    };
});
builder.Services.AddInMemoryRateLimiting();
```

---

### 5.4 中期対応（2週間〜1ヶ月）

#### パスワードハッシュ化の実装

```csharp
public class LocalAuthService : ILocalAuthService
{
    public async Task<bool> ValidateCredentialsAsync(string email, string password)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null) return false;

        // BCrypt でハッシュ検証
        return BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
    }
}
```

#### Cookie セキュリティの強化

```csharp
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/SignIn";
        options.LogoutPath = "/Auth/Logout";
        options.ExpireTimeSpan = TimeSpan.FromHours(1);
        options.SlidingExpiration = true;

        // セキュリティ強化
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.IsEssential = true;
        options.Cookie.Name = "__Host-Auth";  // Cookie Prefix
    });
```

---

## 6. 詳細分析

### 6.1 データフロー分析

#### ユーザーパスワードのフロー

```
ブラウザ (HTTPS)
    ↓ POST body
SignIn.cshtml
    ↓ model binding
AuthController.SignIn()
    ↓ 引数渡し
LocalAuthService.ValidateCredentialsAsync()
    ↓ メモリ内比較（平文）
結果を返却
    ↓
パスワードはメモリから廃棄
```

**評価**: パスワードは一時的だが、平文比較は危険

---

#### Session トークンのフロー

```
Cognito InitiateAuth 応答
    ↓
AuthController で URL パラメータとして通知 ← 問題
    ↓
OtpChallengeViewModel に格納
    ↓
hidden input に格納
    ↓
POST で再送信
    ↓
RespondToAuthChallenge で使用
```

**評価**: URL 露出が最大の問題

---

### 6.2 脅威モデル

| 脅威 | 攻撃ベクトル | 現在の保護 | リスクレベル |
|------|-------------|------------|--------------|
| 認証情報漏洩 | ソースコード公開 | なし | 🔴 Critical |
| Session ハイジャック | URL 傍受 | HTTPS のみ | 🟠 High |
| ブルートフォース | OTP 試行 | Cognito Rate Limit | 🟠 High |
| タイミング攻撃 | レスポンス時間計測 | なし | 🟡 Medium |
| CSRF | 偽造リクエスト | AntiForgeryToken | 🟢 Low |
| XSS | スクリプト注入 | Razor エンコード | 🟢 Low |

---

### 6.3 コンプライアンス評価

| 基準 | 項目 | 状態 |
|------|------|------|
| OWASP Top 10 | A01: アクセス制御の不備 | ✅ 認可実装済み |
| OWASP Top 10 | A02: 暗号化の失敗 | ❌ 平文パスワード |
| OWASP Top 10 | A03: インジェクション | ✅ パラメータ化 |
| OWASP Top 10 | A04: 安全でない設計 | ⚠️ 二重ユーザー管理 |
| OWASP Top 10 | A05: セキュリティ設定のミス | ❌ 秘密情報のハードコード |
| OWASP Top 10 | A07: 認証の不備 | ⚠️ Rate Limiting なし |
| PCI DSS | 要件 3.4 | ❌ 平文パスワード保存 |
| PCI DSS | 要件 8.2.1 | ❌ 強力な暗号化なし |

---

## 付録

### A. 検査対象ファイル一覧

| ファイル | 検査内容 |
|----------|----------|
| `Controllers/AuthController.cs` | 認証フロー、Session 管理 |
| `Services/LocalAuthService.cs` | パスワード検証 |
| `Services/CognitoEmailOtpService.cs` | Cognito API 呼び出し |
| `Models/SignInViewModel.cs` | 入力バリデーション |
| `Models/OtpChallengeViewModel.cs` | 入力バリデーション |
| `Program.cs` | DI 設定、認証設定 |
| `appsettings.json` | 設定テンプレート |
| `appsettings.Development.json` | 開発環境設定 |

### B. 使用ツール

- 手動コードレビュー
- 静的解析

### C. 参考資料

- [OWASP Top 10 2021](https://owasp.org/Top10/)
- [OWASP Authentication Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Authentication_Cheat_Sheet.html)
- [AWS Cognito Security Best Practices](https://docs.aws.amazon.com/cognito/latest/developerguide/security.html)
- [ASP.NET Core Security Documentation](https://learn.microsoft.com/en-us/aspnet/core/security/)

---

**レビュー担当**: Claude Code
**次回レビュー推奨日**: 修正完了後
