namespace AppointMe.Api.Antiforgery;

internal static class AntiforgeryExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddAppointMeAntiforgery()
        {
            return services.AddAntiforgery(options =>
            {
                options.HeaderName = AntiforgeryMiddleware.RequestTokenHeaderName;
                options.Cookie.Name = "appointme.csrf";
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            });
        }
    }

    extension(IApplicationBuilder app)
    {
        public IApplicationBuilder UseAppointMeAntiforgery()
        {
            return app.UseMiddleware<AntiforgeryMiddleware>();
        }
    }
}
