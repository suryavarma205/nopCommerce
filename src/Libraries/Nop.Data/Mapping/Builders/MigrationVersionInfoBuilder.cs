using FluentMigrator.Builders.Create.Table;
using Nop.Data.Migrations;

namespace Nop.Data.Mapping.Builders
{
    /// <summary>
    /// Represents a migration version info entity builder
    /// </summary>
    public partial class MigrationVersionInfoBuilder : NopEntityBuilder<MigrationVersionInfo>
    {
        public override void MapEntity(CreateTableExpressionBuilder table)
        {
            table
                .WithColumn(nameof(MigrationVersionInfo.Version)).AsInt64().PrimaryKey();
        }
    }
}
