using FluentMigrator;
using Nop.Core.Domain.Logging;

namespace Nop.Data.Migrations.UpgradeTo440
{
    [NopMigration("2020-06-10 00:00:00", "4.40.0", UpdateMigrationType.Data)]
    [SkipMigrationOnInstall]
    public class DataMigration : MigrationBase
    {
        #region Fields

        private readonly IRepository<ActivityLogType> _activityLogTypeRepository;

        #endregion

        #region Ctor

        public DataMigration(IRepository<ActivityLogType> activityLogTypeRepository)
        {
            _activityLogTypeRepository = activityLogTypeRepository;
        }

        #endregion

        /// <summary>
        /// Collect the UP migration expressions
        /// </summary>
        public override void Up()
        {
            var activityLogTypes = new ActivityLogType[]
            {
                new ActivityLogType
                {
                    SystemKeyword = "AddNewSpecAttributeGroup",
                    Enabled = true,
                    Name = "Add a new specification attribute group"
                },
                new ActivityLogType
                {
                    SystemKeyword = "EditSpecAttributeGroup",
                    Enabled = true,
                    Name = "Edit a specification attribute group"
                },
                new ActivityLogType
                {
                    SystemKeyword = "DeleteSpecAttributeGroup",
                    Enabled = true,
                    Name = "Delete a specification attribute group"
                }
            };
            _activityLogTypeRepository.Insert(activityLogTypes);
        }

        public override void Down()
        {
            //add the downgrade logic if necessary 
        }
    }
}
