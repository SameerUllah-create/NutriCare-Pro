namespace NutriCare.Tests.Configuration;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NutriCare.Persistence.Configuration;
using NutriCare.Persistence.Contexts;

/// <summary>Opt-in recovery rehearsal. SQL Server must be able to write the configured backup directory.</summary>
[Trait("Category","SqlIntegration")]
public class SqlRecoveryTests(IsolatedSqlDatabase fixture):IClassFixture<IsolatedSqlDatabase>
{
    [RecoveryFact]
    public async Task Backup_restore_then_additive_upgrade_preserves_drafts_and_clinical_schema()
    {
        var backupDirectory=Path.GetFullPath(Environment.GetEnvironmentVariable("NUTRICARE_TEST_BACKUP_DIRECTORY")!);
        Directory.CreateDirectory(backupDirectory);
        var restoredName="NutriCareTest_"+Guid.NewGuid().ToString("N");
        var backupPath=Path.GetFullPath(Path.Combine(backupDirectory,restoredName+".bak"));
        if(!backupPath.StartsWith(backupDirectory+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Unsafe test backup target");
        var sourceBuilder=new SqlConnectionStringBuilder(fixture.ConnectionString);
        var sourceName=sourceBuilder.InitialCatalog;
        ValidateName(sourceName);ValidateName(restoredName);
        var restoredBuilder=new SqlConnectionStringBuilder(fixture.ConnectionString){InitialCatalog=restoredName};
        var restoredOptions=new DbContextOptionsBuilder<NutriCareDbContext>().UseSqlServer(restoredBuilder.ConnectionString).Options;
        var masterBuilder=new SqlConnectionStringBuilder(fixture.ConnectionString){InitialCatalog="master"};
        await using var admin=new SqlConnection(masterBuilder.ConnectionString);await admin.OpenAsync();
        try
        {
            await using(var source=fixture.Open())
            {
                // Rehearse the real additive upgrade from the first tracked schema.
                await source.GetService<IMigrator>().MigrateAsync(source.Database.GetMigrations().First());
                // Use the historical column set, not current entities, before upgrading.
                await source.Database.ExecuteSqlRawAsync("""
                    INSERT INTO Services (Name,NameUrdu,DurationMinutes,DeliveryHours,Mode,Price,Currency,IsActive,IsPublished,BookingBufferMinutes,Version,CreatedAt,UpdatedAt)
                    SELECT n,N'پرانا مسودہ',m,h,CASE WHEN h IS NULL THEN 'Online' ELSE 'Async' END,p,'PKR',0,0,15,1,SYSUTCDATETIME(),SYSUTCDATETIME()
                    FROM (VALUES ('Initial Nutrition Consultation',45,2500,CAST(NULL AS int)),('Weight Loss',45,2500,NULL),('Weight Gain',45,2500,NULL),
                    ('Diabetes Nutrition',45,3000,NULL),('PCOS Nutrition',45,3000,NULL),('Pregnancy Nutrition',45,3000,NULL),('Child Nutrition',45,2500,NULL),
                    ('Sports Nutrition',60,3500,NULL),('Follow-up',20,1500,NULL),('Personalized Diet Plan',0,2000,48)) AS proposal(n,m,p,h);
                    INSERT INTO ServicePackages (Name,NameUrdu,DurationWeeks,Price,Currency,IsPublished,IsActive,Version,CreatedAt,UpdatedAt)
                    SELECT n,N'پرانا پیکیج',w,p,'PKR',0,0,1,SYSUTCDATETIME(),SYSUTCDATETIME()
                    FROM (VALUES ('Starter',2,4250),('Monthly',4,7500),('Transformation',12,18000)) AS proposal(n,w,p);
                    INSERT INTO AvailabilityRules (NutritionistId,DayOfWeek,StartTime,EndTime,TimeZoneId,IsPublished,BookingBufferMinutes,Version,CreatedAt,UpdatedAt)
                    SELECT 0,d,CAST('10:00' AS time),CAST(CASE WHEN d=6 THEN '14:00' ELSE '18:00' END AS time),'Asia/Karachi',0,15,1,SYSUTCDATETIME(),SYSUTCDATETIME()
                    FROM (VALUES (1),(2),(3),(4),(5),(6)) AS days(d);
                    INSERT INTO CancellationPolicies (Name,Status,IsPublished,FullRefundHours,FreeRescheduleHours,MaxFreeReschedules,ProviderCancellationRefund,NoShowAutomaticRefund,Version,CreatedAt,UpdatedAt)
                    VALUES ('Proposed cancellation policy','Draft',0,24,6,1,1,0,1,SYSUTCDATETIME(),SYSUTCDATETIME());
                    INSERT INTO ConfigurationRevisions (EntityType,EntityId,Version,Action,SnapshotJson,ChangedByUserId,CreatedAt,UpdatedAt)
                    VALUES ('seed',0,1,'Proposed configuration v1','{{"approved":false}}',0,SYSUTCDATETIME(),SYSUTCDATETIME());
                    """);
            }
            await using(var backup=new SqlCommand($"BACKUP DATABASE [{sourceName}] TO DISK=@path WITH COPY_ONLY, INIT, CHECKSUM; RESTORE VERIFYONLY FROM DISK=@path WITH CHECKSUM;",admin))
            {backup.CommandTimeout=120;backup.Parameters.AddWithValue("@path",backupPath);await backup.ExecuteNonQueryAsync();}
            string logicalData,logicalLog,dataPath,logPath;
            await using(var source=new SqlConnection(fixture.ConnectionString))
            {
                await source.OpenAsync();
                await using var command=new SqlCommand("SELECT name,type FROM sys.database_files WHERE type IN (0,1) ORDER BY type;",source);
                await using var reader=await command.ExecuteReaderAsync();await reader.ReadAsync();logicalData=reader.GetString(0);await reader.ReadAsync();logicalLog=reader.GetString(0);
            }
            await using(var locations=new SqlCommand("SELECT CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultDataPath')),CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultLogPath'));",admin))
            {await using var reader=await locations.ExecuteReaderAsync();await reader.ReadAsync();dataPath=reader.GetString(0);logPath=reader.IsDBNull(1)?dataPath:reader.GetString(1);}
            static string Literal(string value)=>"N'"+value.Replace("'","''")+"'";
            static string ServerFile(string directory,string file)=>directory.TrimEnd('/','\\')+(directory.Contains('\\')?"\\":"/")+file;
            var restoreSql=$"IF DB_ID(N'{restoredName}') IS NOT NULL THROW 51000,'Test restore database already exists',1; RESTORE DATABASE [{restoredName}] FROM DISK=@path WITH MOVE {Literal(logicalData)} TO {Literal(ServerFile(dataPath,restoredName+".mdf"))}, MOVE {Literal(logicalLog)} TO {Literal(ServerFile(logPath,restoredName+"_log.ldf"))}, RECOVERY, CHECKSUM;";
            await using(var restore=new SqlCommand(restoreSql,admin)){restore.CommandTimeout=120;restore.Parameters.AddWithValue("@path",backupPath);await restore.ExecuteNonQueryAsync();}
            await using var restored=new NutriCareDbContext(restoredOptions);
            await restored.Database.MigrateAsync();
            await ProposedConfigurationSeeder.SeedAsync(restored);
            Assert.Equal(10,await restored.Services.CountAsync());Assert.Equal(6,await restored.ServicePackages.CountAsync());
            Assert.True(await restored.ServicePackages.AnyAsync(p=>p.Name=="Starter" && p.Price==4250 && p.DurationDays==0));
            Assert.True(await restored.ServicePackages.AnyAsync(p=>p.Name=="Starter" && p.Price==4000 && p.DurationDays==14 && p.InitialConsultationCount==1 && p.FollowUpConsultationCount==1));
            Assert.False(await restored.Services.AnyAsync(s=>s.IsActive || s.IsPublished));
            Assert.False(await restored.ServicePackages.AnyAsync(p=>p.IsActive || p.IsPublished || p.NutritionistApprovedAt!=null));
            Assert.Equal(6, await restored.AvailabilityRules.CountAsync());
            Assert.Equal(1, await restored.CancellationPolicies.CountAsync());
            Assert.False(await restored.AvailabilityRules.AnyAsync(r=>r.IsPublished));
            Assert.True(await restored.CancellationPolicies.AllAsync(p=>!p.IsPublished && p.PackageNoShowCreditAction=="ReviewRequired" && p.PackagePatientCancellationCreditAction=="ReviewRequired"));
            Assert.Equal(restored.Database.GetMigrations().Count(),(await restored.Database.GetAppliedMigrationsAsync()).Count());
            // This query references the new columns and therefore proves the additive schema is present.
            Assert.Empty(await restored.ConsentRecords.Select(c=>new{c.PolicyTitle,c.ConsentTextUrdu,c.PresentedLanguage}).ToListAsync());
            Assert.Empty(await restored.Appointments.Select(a=>a.BookingBufferMinutesSnapshot).ToListAsync());
            Assert.Empty(await restored.PackageEnrollments.ToListAsync());
            Assert.Empty(await restored.CancellationRefundInstructions.ToListAsync());
        }
        finally
        {
            ValidateName(restoredName);
            await using var cleanup=new NutriCareDbContext(restoredOptions);await cleanup.Database.EnsureDeletedAsync();
            if(File.Exists(backupPath))File.Delete(backupPath);
        }
    }
    private static void ValidateName(string name)
    {
        if(name.Length!=46 || !name.StartsWith("NutriCareTest_",StringComparison.Ordinal) || !Guid.TryParseExact(name[14..],"N",out _))
            throw new InvalidOperationException("Refusing to alter a database outside the generated test namespace");
    }
}
public sealed class RecoveryFactAttribute:FactAttribute
{
    public RecoveryFactAttribute(){if(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("NUTRICARE_TEST_BACKUP_DIRECTORY")))Skip="Opt in with a backup directory writable by SQL Server; see development setup";}
}
