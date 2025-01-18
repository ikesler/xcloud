using System.Text;
using Microsoft.AspNetCore.Localization;
using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Tokens;
using XCloud.Api.Settings;
using XCloud.Core;

namespace XCloud.Api;

public class Module: IModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        var securitySettings = configuration
            .GetRequiredSection(nameof(SecuritySettings))
            .Get<SecuritySettings>() ?? throw new Exception("Missing sequrity settings");

        services.AddAuthentication().AddJwtBearer(o =>
        {
            IdentityModelEventSource.ShowPII = true;
            o.TokenValidationParameters.IssuerSigningKey =
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(securitySettings.JwtSignKey));
            o.TokenValidationParameters.ValidateActor = false;
            o.TokenValidationParameters.ValidateAudience = false;
            o.TokenValidationParameters.ValidateTokenReplay = false;
            o.TokenValidationParameters.ValidateIssuer = false;
            o.TokenValidationParameters.ValidateLifetime = true;
            o.TokenValidationParameters.ValidateIssuerSigningKey = true;
        });
        services.AddAuthorization();
        services.AddLocalization(options => options.ResourcesPath = "Resources");
        services.AddMvc().AddViewLocalization();
        services.Configure<RequestLocalizationOptions>(options =>
        {
            options.RequestCultureProviders.Clear();
            options.RequestCultureProviders = [
                new CustomRequestCultureProvider(context =>
                {
                    var languages = context.Request.Headers.AcceptLanguage.ToString();
                    var firstLang = languages.Split(',').FirstOrDefault();
                    var defaultLang = string.IsNullOrEmpty(firstLang) ? "en" : firstLang;
                    var result = new ProviderCultureResult(defaultLang, defaultLang);
                    Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo(defaultLang);
                    Thread.CurrentThread.CurrentUICulture = Thread.CurrentThread.CurrentCulture;

                    return Task.FromResult(result)!;
                })
            ];
        });
        services.AddControllersWithViews();
        services.AddOptions<SecuritySettings>().Bind(configuration.GetSection(nameof(SecuritySettings)));
        services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
    }
}