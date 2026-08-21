namespace AppointMe.Shared.Companies;

public static class CompanyIdExtensions
{
    extension(CompanyId companyId)
    {
        public string ToTenantId()
        {
            return companyId.Value.ToString();
        }
    }
}
