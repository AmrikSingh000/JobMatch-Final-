using Microsoft.AspNetCore.Identity;

namespace JobMatch.Data.Seed
{
    public static class IdentitySeed
    {

        public static async Task EnsureSeedAsync(IServiceProvider sp)
        {
            var roles = new[] { "Administrator", "Recruiter", "Jobseeker" };
            var roleMgr = sp.GetRequiredService<RoleManager<IdentityRole>>();
            foreach (var r in roles)
                if (!await roleMgr.RoleExistsAsync(r))
                    await roleMgr.CreateAsync(new IdentityRole(r));

            var userMgr = sp.GetRequiredService<UserManager<IdentityUser>>();

            // --- First admin ---
            var adminEmail1 = "admin@jobmatch.local";
            var admin1 = await userMgr.FindByEmailAsync(adminEmail1);
            if (admin1 == null)
            {
                admin1 = new IdentityUser
                {
                    UserName = adminEmail1,
                    Email = adminEmail1,
                    EmailConfirmed = true
                };

                await userMgr.CreateAsync(admin1, "Admin!123");
                await userMgr.AddToRoleAsync(admin1, "Administrator");
            }

            // --- Second admin ---
            var adminEmail2 = "admin2@jobmatch.local";
            var admin2 = await userMgr.FindByEmailAsync(adminEmail2);
            if (admin2 == null)
            {
                admin2 = new IdentityUser
                {
                    UserName = adminEmail2,
                    Email = adminEmail2,
                    EmailConfirmed = true
                };

                await userMgr.CreateAsync(admin2, "Admin2!123");
                await userMgr.AddToRoleAsync(admin2, "Administrator");
            }
        }
    }
}
