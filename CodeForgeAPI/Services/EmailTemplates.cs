using System.Net;

namespace CodeForgeAPI.Services;

/// <summary>HTML-шаблоны писем CodeForge (тёмная тема, table-layout для совместимости с почтовыми клиентами)</summary>
public static class EmailTemplates
{
    public const string VerificationSubject = "CodeForge — код подтверждения регистрации";
    public const string PasswordResetSubject = "CodeForge — сброс пароля";
    public const string WelcomeSubject = "CodeForge — добро пожаловать!";

    public static string Welcome(string firstName) =>
        BuildWelcomeLayout(
            preheader: "Регистрация в CodeForge прошла успешно — добро пожаловать!",
            badge: "Добро пожаловать",
            badgeColor: "#10b981",
            title: $"Здравствуйте, {firstName}!",
            subtitle: "Ваш аккаунт успешно создан. Теперь вы можете проектировать схемы данных и генерировать готовый backend-код за несколько минут.",
            bodyHtml: """
                <p style="margin:0 0 16px;font-size:14px;line-height:1.65;color:#94a3b8;">
                  Пожалуйста, ознакомьтесь с возможностями платформы:
                </p>
                <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="margin-bottom:8px;">
                  <tr>
                    <td style="padding:12px 14px;background-color:#0a0a0f;border:1px solid #1e1e2e;border-radius:10px;">
                      <div style="font-size:13px;font-weight:600;color:#e2e8f0;margin-bottom:4px;">✨ Шаблоны проектов</div>
                      <div style="font-size:13px;line-height:1.55;color:#64748b;">Готовые схемы для магазина, блога, CRM и других тем</div>
                    </td>
                  </tr>
                </table>
                <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="margin-bottom:8px;">
                  <tr>
                    <td style="padding:12px 14px;background-color:#0a0a0f;border:1px solid #1e1e2e;border-radius:10px;">
                      <div style="font-size:13px;font-weight:600;color:#e2e8f0;margin-bottom:4px;">🔗 ER-диаграмма и связи</div>
                      <div style="font-size:13px;line-height:1.55;color:#64748b;">Визуальное проектирование сущностей и отношений между ними</div>
                    </td>
                  </tr>
                </table>
                <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="margin-bottom:8px;">
                  <tr>
                    <td style="padding:12px 14px;background-color:#0a0a0f;border:1px solid #1e1e2e;border-radius:10px;">
                      <div style="font-size:13px;font-weight:600;color:#e2e8f0;margin-bottom:4px;">⚙️ Генерация кода</div>
                      <div style="font-size:13px;line-height:1.55;color:#64748b;">C# + PostgreSQL или Node.js + MongoDB — монолит или микросервисы</div>
                    </td>
                  </tr>
                </table>
                <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0">
                  <tr>
                    <td style="padding:12px 14px;background-color:#0a0a0f;border:1px solid #1e1e2e;border-radius:10px;">
                      <div style="font-size:13px;font-weight:600;color:#e2e8f0;margin-bottom:4px;">🏆 Профиль и достижения</div>
                      <div style="font-size:13px;line-height:1.55;color:#64748b;">Статистика, история генераций и система достижений</div>
                    </td>
                  </tr>
                </table>
                """,
            hint: "Войдите в аккаунт и создайте первый проект — в интерфейсе есть подсказки и FAQ.",
            footerNote: "Спасибо, что выбрали CodeForge. Приятной работы!"
        );

    public static string VerificationCode(string code) =>
        BuildLayout(
            preheader: "Ваш код подтверждения для регистрации в CodeForge",
            badge: "Регистрация",
            badgeColor: "#6366f1",
            title: "Подтвердите регистрацию",
            subtitle: "Введите код ниже на странице регистрации, чтобы завершить создание аккаунта.",
            code: code,
            hint: "Код действителен <strong style=\"color:#e2e8f0;\">15 минут</strong>. Если вы не запрашивали регистрацию — просто проигнорируйте это письмо.",
            footerNote: "Это автоматическое сообщение от платформы CodeForge."
        );

    public static string PasswordResetCode(string code) =>
        BuildLayout(
            preheader: "Код для сброса пароля CodeForge",
            badge: "Безопасность",
            badgeColor: "#ef4444",
            title: "Сброс пароля",
            subtitle: "Вы запросили восстановление доступа к аккаунту. Используйте код ниже на странице сброса пароля.",
            code: code,
            hint: "Код действителен <strong style=\"color:#e2e8f0;\">15 минут</strong>. Если вы не запрашивали сброс — немедленно смените пароль и включите 2FA.",
            footerNote: "Никому не сообщайте этот код."
        );

    private static string BuildLayout(
        string preheader,
        string badge,
        string badgeColor,
        string title,
        string subtitle,
        string code,
        string hint,
        string footerNote)
    {
        var safeCode = WebUtility.HtmlEncode(code);
        var safeTitle = WebUtility.HtmlEncode(title);
        var safeSubtitle = WebUtility.HtmlEncode(subtitle);
        var safeBadge = WebUtility.HtmlEncode(badge);
        var safePreheader = WebUtility.HtmlEncode(preheader);

        return $@"<!DOCTYPE html>
<html lang=""ru"">
<head>
  <meta charset=""UTF-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
  <meta http-equiv=""X-UA-Compatible"" content=""IE=edge"">
  <title>{safeTitle}</title>
  <!--[if mso]><style>table,td{{font-family:Arial,sans-serif!important;}}</style><![endif]-->
</head>
<body style=""margin:0;padding:0;background-color:#09090f;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,'Helvetica Neue',Arial,sans-serif;-webkit-font-smoothing:antialiased;"">
  <span style=""display:none!important;visibility:hidden;opacity:0;height:0;width:0;overflow:hidden;"">{safePreheader}</span>

  <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"" style=""background-color:#09090f;min-height:100vh;"">
    <tr>
      <td align=""center"" style=""padding:40px 16px;"">

        <!-- Верхняя декоративная линия -->
        <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"" style=""max-width:520px;"">
          <tr>
            <td style=""height:3px;background:linear-gradient(90deg,#6366f1,#8b5cf6,#6366f1);border-radius:3px 3px 0 0;""></td>
          </tr>
        </table>

        <!-- Основная карточка -->
        <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"" style=""max-width:520px;background-color:#12121a;border:1px solid #1e1e2e;border-top:none;border-radius:0 0 16px 16px;overflow:hidden;"">

          <!-- Шапка -->
          <tr>
            <td style=""padding:32px 32px 24px;text-align:center;background-color:#12121a;"">
              <div style=""font-size:22px;font-weight:800;color:#ffffff;letter-spacing:-0.5px;line-height:1;"">CodeForge</div>
              <div style=""font-size:11px;color:#64748b;text-transform:uppercase;letter-spacing:0.12em;margin-top:4px;font-weight:600;"">Генерация backend-кода</div>
            </td>
          </tr>

          <!-- Разделитель -->
          <tr>
            <td style=""padding:0 32px;"">
              <div style=""height:1px;background-color:#1e1e2e;""></div>
            </td>
          </tr>

          <!-- Контент -->
          <tr>
            <td style=""padding:28px 32px 32px;"">
              <div style=""display:inline-block;padding:4px 12px;border-radius:999px;background-color:{badgeColor}22;border:1px solid {badgeColor}44;color:{badgeColor};font-size:11px;font-weight:700;text-transform:uppercase;letter-spacing:0.08em;margin-bottom:16px;"">{safeBadge}</div>

              <h1 style=""margin:0 0 12px;font-size:24px;font-weight:700;color:#f8fafc;line-height:1.3;letter-spacing:-0.02em;"">{safeTitle}</h1>
              <p style=""margin:0 0 28px;font-size:15px;line-height:1.65;color:#94a3b8;"">{safeSubtitle}</p>

              <!-- Блок с кодом -->
              <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"">
                <tr>
                  <td align=""center"" style=""padding:24px 16px;background-color:#0a0a0f;border:1px solid #1e1e2e;border-radius:12px;"">
                    <div style=""font-size:11px;color:#64748b;text-transform:uppercase;letter-spacing:0.15em;font-weight:600;margin-bottom:12px;"">Ваш код</div>
                    <div style=""font-size:36px;font-weight:800;color:#ffffff;letter-spacing:0.35em;font-family:'Courier New',Courier,monospace;padding-left:0.35em;"">{safeCode}</div>
                  </td>
                </tr>
              </table>

              <p style=""margin:24px 0 0;font-size:13px;line-height:1.6;color:#64748b;text-align:center;"">{hint}</p>
            </td>
          </tr>

          <!-- Подвал карточки -->
          <tr>
            <td style=""padding:20px 32px 28px;background-color:#0d0d14;border-top:1px solid #1e1e2e;"">
              <p style=""margin:0;font-size:12px;line-height:1.5;color:#475569;text-align:center;"">{footerNote}</p>
            </td>
          </tr>
        </table>

        <!-- Нижний текст -->
        <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"" style=""max-width:520px;"">
          <tr>
            <td style=""padding:20px 16px 0;text-align:center;"">
              <p style=""margin:0;font-size:11px;color:#334155;line-height:1.5;"">
                © {DateTime.UtcNow.Year} CodeForge · Генератор backend-проектов
              </p>
            </td>
          </tr>
        </table>

      </td>
    </tr>
  </table>
</body>
</html>";
    }

    private static string BuildWelcomeLayout(
        string preheader,
        string badge,
        string badgeColor,
        string title,
        string subtitle,
        string bodyHtml,
        string hint,
        string footerNote)
    {
        var safeTitle = WebUtility.HtmlEncode(title);
        var safeSubtitle = WebUtility.HtmlEncode(subtitle);
        var safeBadge = WebUtility.HtmlEncode(badge);
        var safePreheader = WebUtility.HtmlEncode(preheader);
        var safeHint = hint;
        var safeFooter = WebUtility.HtmlEncode(footerNote);

        return $@"<!DOCTYPE html>
<html lang=""ru"">
<head>
  <meta charset=""UTF-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
  <meta http-equiv=""X-UA-Compatible"" content=""IE=edge"">
  <title>{safeTitle}</title>
  <!--[if mso]><style>table,td{{font-family:Arial,sans-serif!important;}}</style><![endif]-->
</head>
<body style=""margin:0;padding:0;background-color:#09090f;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,'Helvetica Neue',Arial,sans-serif;-webkit-font-smoothing:antialiased;"">
  <span style=""display:none!important;visibility:hidden;opacity:0;height:0;width:0;overflow:hidden;"">{safePreheader}</span>

  <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"" style=""background-color:#09090f;min-height:100vh;"">
    <tr>
      <td align=""center"" style=""padding:40px 16px;"">

        <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"" style=""max-width:520px;"">
          <tr>
            <td style=""height:3px;background:linear-gradient(90deg,#6366f1,#8b5cf6,#6366f1);border-radius:3px 3px 0 0;""></td>
          </tr>
        </table>

        <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"" style=""max-width:520px;background-color:#12121a;border:1px solid #1e1e2e;border-top:none;border-radius:0 0 16px 16px;overflow:hidden;"">

          <tr>
            <td style=""padding:32px 32px 24px;text-align:center;background-color:#12121a;"">
              <div style=""font-size:22px;font-weight:800;color:#ffffff;letter-spacing:-0.5px;line-height:1;"">CodeForge</div>
              <div style=""font-size:11px;color:#64748b;text-transform:uppercase;letter-spacing:0.12em;margin-top:4px;font-weight:600;"">Генерация backend-кода</div>
            </td>
          </tr>

          <tr>
            <td style=""padding:0 32px;"">
              <div style=""height:1px;background-color:#1e1e2e;""></div>
            </td>
          </tr>

          <tr>
            <td style=""padding:28px 32px 32px;"">
              <div style=""display:inline-block;padding:4px 12px;border-radius:999px;background-color:{badgeColor}22;border:1px solid {badgeColor}44;color:{badgeColor};font-size:11px;font-weight:700;text-transform:uppercase;letter-spacing:0.08em;margin-bottom:16px;"">{safeBadge}</div>

              <h1 style=""margin:0 0 12px;font-size:24px;font-weight:700;color:#f8fafc;line-height:1.3;letter-spacing:-0.02em;"">{safeTitle}</h1>
              <p style=""margin:0 0 24px;font-size:15px;line-height:1.65;color:#94a3b8;"">{safeSubtitle}</p>

              {bodyHtml}

              <p style=""margin:24px 0 0;font-size:13px;line-height:1.6;color:#64748b;text-align:center;"">{safeHint}</p>
            </td>
          </tr>

          <tr>
            <td style=""padding:20px 32px 28px;background-color:#0d0d14;border-top:1px solid #1e1e2e;"">
              <p style=""margin:0;font-size:12px;line-height:1.5;color:#475569;text-align:center;"">{safeFooter}</p>
            </td>
          </tr>
        </table>

        <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"" style=""max-width:520px;"">
          <tr>
            <td style=""padding:20px 16px 0;text-align:center;"">
              <p style=""margin:0;font-size:11px;color:#334155;line-height:1.5;"">
                © {DateTime.UtcNow.Year} CodeForge · Генератор backend-проектов
              </p>
            </td>
          </tr>
        </table>

      </td>
    </tr>
  </table>
</body>
</html>";
    }
}
