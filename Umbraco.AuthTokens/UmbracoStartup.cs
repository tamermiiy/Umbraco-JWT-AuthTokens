using System.Linq;
using Umbraco.Core;
using Umbraco.Core.Composing;
using Umbraco.Core.Events;
using Umbraco.Core.Migrations;
using Umbraco.Core.Migrations.Upgrade;
using Umbraco.Core.Models;
using Umbraco.Core.Models.Membership;
using Umbraco.Core.Logging;
using Umbraco.Core.Scoping;
using Umbraco.Core.Services;
using Umbraco.Core.Services.Implement;
using Umbraco.Web;
using UmbracoAuthTokens.Data;


namespace UmbracoAuthTokens
{
    [RuntimeLevel(MinLevel = RuntimeLevel.Run)]
    public class UmbracoStartupComposer : IUserComposer
    {
        public void Compose(Composition composition)
        {
            //composition.SetContentLastChanceFinder<My404ContentFinder>();

            composition.Components().Append<UmbracoStartup>();
        }
    }

    public class UmbracoStartup : IComponent
    {
        private IScopeProvider _scopeProvider;
        private IMigrationBuilder _migrationBuilder;
        private IKeyValueService _keyValueService;
        private ILogger _logger;

        public UmbracoStartup(IScopeProvider scopeProvider, IMigrationBuilder migrationBuilder, IKeyValueService keyValueService, ILogger logger)
        {
            _scopeProvider = scopeProvider;
            _migrationBuilder = migrationBuilder;
            _keyValueService = keyValueService;
            _logger = logger;
        }

        public void Initialize()
        {
            // Create a migration plan for a specific project/feature
            // We can then track that latest migration state/step for this project/feature
            var migrationPlan = new MigrationPlan("identityAuthTokens");

            // This is the steps we need to take
            // Each step in the migration adds a unique value
            migrationPlan.From(string.Empty)
                .To<AddUmbracoAuthTokenTable>("identityAuthTokens-db");

            // Go and upgrade our site (Will check if it needs to do the work or not)
            // Based on the current/latest step
            var upgrader = new Upgrader(migrationPlan);
            upgrader.Execute(_scopeProvider, _migrationBuilder, _keyValueService, _logger);

            //Add event to saving/chaning pasword on Umbraco backoffice user
            UserService.SavingUser += UserService_SavingUser;

            //Add event to saving/chaning pasword on Umbraco member
            MemberService.Saving += MemberService_Saving;

            //ContentService.Saving += this.ContentService_Saving;
        }

        public void Terminate()
        {
            //Add event to saving/chaning pasword on Umbraco backoffice user
            UserService.SavingUser -= UserService_SavingUser;

            //Add event to saving/chaning pasword on Umbraco member
            MemberService.Saving -= MemberService_Saving;

            //unsubscribe during shutdown
            //ContentService.Saving -= this.ContentService_Saving;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void MemberService_Saving(IMemberService sender, SaveEventArgs<IMember> e)
        {
            //Saved entites (Could be more than one member saved. Very unlikely?)
            var member = e.SavedEntities.FirstOrDefault();

            //Found a member that has been saved
            if (member != null)
            {
                //Check if the password property (RawPasswordValue) is dirty aka has beeen changed
                var passIsDirty = member.IsPropertyDirty("RawPasswordValue");

                //Password has been changed
                if (passIsDirty)
                {
                    //Check if user already has token in DB (token created on first login/auth to API)
                    var hasAuthToken = UserAuthTokenDbHelper.GetAuthToken(member.Id);

                    //invalidate token (Only if token exists in DB)
                    //We have found an existing token
                    if (hasAuthToken != null)
                    {
                        //Generate AuthToken DB object
                        var newToken = new UmbracoAuthToken();
                        newToken.IdentityId = member.Id;
                        newToken.IdentityType = IdentityAuthType.Member.ToString();

                        //Generate a new token for the user
                        var authToken = UmbracoAuthTokenFactory.GenerateUserAuthToken(newToken);

                        //NOTE: We insert authToken as opposed to newToken
                        //As authToken now has DateTime & JWT token string on it now

                        //Store in DB (inserts or updates existing)
                        UserAuthTokenDbHelper.InsertAuthToken(authToken);
                    }
                }
            }
        }


        /// <summary>
        /// When we save a user, let's check if backoffice user has changed their password
        /// </summary>
        void UserService_SavingUser(IUserService sender, SaveEventArgs<IUser> e)
        {
            //Saved entites (Could be more than one user saved. Very unlikely?)
            var user = e.SavedEntities.FirstOrDefault();

            //Found a user that has been saved
            if (user != null)
            {
                //Check if the password property (RawPasswordValue) is dirty aka has beeen changed
                var passIsDirty = user.IsPropertyDirty("RawPasswordValue");

                //Password has been changed
                if (passIsDirty)
                {
                    //Check if user already has token in DB (token created on first login/auth to API)
                    var hasAuthToken = UserAuthTokenDbHelper.GetAuthToken(user.Id);

                    //invalidate token (Only if token exists in DB)
                    //We have found an existing token
                    if (hasAuthToken != null)
                    {
                        //Generate AuthToken DB object
                        var newToken = new UmbracoAuthToken();
                        newToken.IdentityId = user.Id;
                        newToken.IdentityType = IdentityAuthType.User.ToString();

                        //Generate a new token for the user
                        var authToken = UmbracoAuthTokenFactory.GenerateUserAuthToken(newToken);

                        //NOTE: We insert authToken as opposed to newToken
                        //As authToken now has DateTime & JWT token string on it now

                        //Store in DB (inserts or updates existing)
                        UserAuthTokenDbHelper.InsertAuthToken(authToken);
                    }
                }
            }
        }
    }

    public class AddUmbracoAuthTokenTable : MigrationBase
    {
        public AddUmbracoAuthTokenTable(IMigrationContext context) : base(context)
        {
        }

        public override void Migrate()
        {
            Logger.Debug<AddUmbracoAuthTokenTable>("Running migration {MigrationStep}", "AddUmbracoAuthTokenTable");

            // Lots of methods available in the MigrationBase class - discover with this.
            if (TableExists("identityAuthTokens") == false)
            {
                Create.Table<UmbracoAuthToken>().Do();
            }
            else
            {
                Logger.Debug<AddUmbracoAuthTokenTable>("The database table {DbTable} already exists, skipping", "identityAuthTokens");
            }
        }
    }

}
