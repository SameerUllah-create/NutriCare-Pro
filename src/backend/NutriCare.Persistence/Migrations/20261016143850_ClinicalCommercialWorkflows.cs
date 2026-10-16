using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NutriCare.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ClinicalCommercialWorkflows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PackageServiceCode",
                table: "Services",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActivationRule",
                table: "ServicePackages",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "CreditExpiryDays",
                table: "ServicePackages",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DietPlanCount",
                table: "ServicePackages",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DietPlanRevisionCount",
                table: "ServicePackages",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DurationDays",
                table: "ServicePackages",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "EligibleServiceCodes",
                table: "ServicePackages",
                type: "nvarchar(max)",
                nullable: false,
                defaultValueSql: "'[]'");

            migrationBuilder.AddColumn<int>(
                name: "FirstConsultationWithinDays",
                table: "ServicePackages",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FollowUpConsultationCount",
                table: "ServicePackages",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FollowUpConsultationMinutes",
                table: "ServicePackages",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "InitialConsultationCount",
                table: "ServicePackages",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "InitialConsultationMinutes",
                table: "ServicePackages",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "NoShowCreditRule",
                table: "ServicePackages",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "NutritionistApprovedAt",
                table: "ServicePackages",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "NutritionistApprovedById",
                table: "ServicePackages",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NutritionistApprovedVersion",
                table: "ServicePackages",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProgressTrackingFrequency",
                table: "ServicePackages",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "RestoreCreditsOnProviderCancellation",
                table: "ServicePackages",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LastErrorCode",
                table: "PaymentRefunds",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProcessingToken",
                table: "PaymentRefunds",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProcessingUntil",
                table: "PaymentRefunds",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProviderRequestStartedAt",
                table: "PaymentRefunds",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PackageNoShowCreditAction",
                table: "CancellationPolicies",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "ReviewRequired");

            migrationBuilder.AddColumn<string>(
                name: "PackagePatientCancellationCreditAction",
                table: "CancellationPolicies",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "ReviewRequired");

            migrationBuilder.AddColumn<int>(
                name: "BookingBufferMinutesSnapshot",
                table: "Appointments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PackageCreditKind",
                table: "Appointments",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PackageEnrollmentId",
                table: "Appointments",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CancellationRefundInstructions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AppointmentId = table.Column<long>(type: "bigint", nullable: false),
                    CancelledByUserId = table.Column<long>(type: "bigint", nullable: false),
                    CancellationActor = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PolicyId = table.Column<long>(type: "bigint", nullable: true),
                    PolicyVersion = table.Column<int>(type: "int", nullable: true),
                    PolicySnapshotHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Eligible = table.Column<bool>(type: "bit", nullable: true),
                    DecisionReason = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ExpectedAmount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    PaymentId = table.Column<long>(type: "bigint", nullable: true),
                    PaymentRefundId = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LeaseToken = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    LeaseExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastErrorCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CancellationRefundInstructions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CancellationRefundInstructions_Appointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "Appointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CancellationRefundInstructions_CancellationPolicies_PolicyId",
                        column: x => x.PolicyId,
                        principalTable: "CancellationPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CancellationRefundInstructions_PaymentRefunds_PaymentRefundId",
                        column: x => x.PaymentRefundId,
                        principalTable: "PaymentRefunds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CancellationRefundInstructions_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CancellationRefundInstructions_Users_CancelledByUserId",
                        column: x => x.CancelledByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PackageEnrollments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ServicePackageId = table.Column<long>(type: "bigint", nullable: false),
                    PatientId = table.Column<long>(type: "bigint", nullable: false),
                    PaymentId = table.Column<long>(type: "bigint", nullable: false),
                    PackageVersion = table.Column<int>(type: "int", nullable: false),
                    DefinitionSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PurchasedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FirstConsultationDueAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActivatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackageEnrollments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PackageEnrollments_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PackageEnrollments_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PackageEnrollments_ServicePackages_ServicePackageId",
                        column: x => x.ServicePackageId,
                        principalTable: "ServicePackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PackageCreditUsages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PackageEnrollmentId = table.Column<long>(type: "bigint", nullable: false),
                    CreditKind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ResourceType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ResourceId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ServiceCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackageCreditUsages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PackageCreditUsages_PackageEnrollments_PackageEnrollmentId",
                        column: x => x.PackageEnrollmentId,
                        principalTable: "PackageEnrollments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PackageCreditHistory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PackageEnrollmentId = table.Column<long>(type: "bigint", nullable: false),
                    PackageCreditUsageId = table.Column<long>(type: "bigint", nullable: true),
                    ActorUserId = table.Column<long>(type: "bigint", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    PreviousExpiryUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NewExpiryUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackageCreditHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PackageCreditHistory_PackageCreditUsages_PackageCreditUsageId",
                        column: x => x.PackageCreditUsageId,
                        principalTable: "PackageCreditUsages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PackageCreditHistory_PackageEnrollments_PackageEnrollmentId",
                        column: x => x.PackageEnrollmentId,
                        principalTable: "PackageEnrollments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PackageCreditHistory_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServicePackages_NutritionistApprovedById",
                table: "ServicePackages",
                column: "NutritionistApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_PackageEnrollmentId",
                table: "Appointments",
                column: "PackageEnrollmentId");

            migrationBuilder.CreateIndex(
                name: "IX_CancellationRefundInstructions_AppointmentId",
                table: "CancellationRefundInstructions",
                column: "AppointmentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CancellationRefundInstructions_CancelledByUserId",
                table: "CancellationRefundInstructions",
                column: "CancelledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CancellationRefundInstructions_PaymentId",
                table: "CancellationRefundInstructions",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_CancellationRefundInstructions_PaymentRefundId",
                table: "CancellationRefundInstructions",
                column: "PaymentRefundId");

            migrationBuilder.CreateIndex(
                name: "IX_CancellationRefundInstructions_PolicyId",
                table: "CancellationRefundInstructions",
                column: "PolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_CancellationRefundInstructions_Status_NextAttemptAt",
                table: "CancellationRefundInstructions",
                columns: new[] { "Status", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PackageCreditHistory_ActorUserId",
                table: "PackageCreditHistory",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PackageCreditHistory_PackageCreditUsageId",
                table: "PackageCreditHistory",
                column: "PackageCreditUsageId");

            migrationBuilder.CreateIndex(
                name: "IX_PackageCreditHistory_PackageEnrollmentId_CreatedAt",
                table: "PackageCreditHistory",
                columns: new[] { "PackageEnrollmentId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PackageCreditUsages_PackageEnrollmentId_CreditKind_Status",
                table: "PackageCreditUsages",
                columns: new[] { "PackageEnrollmentId", "CreditKind", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PackageCreditUsages_ResourceType_ResourceId",
                table: "PackageCreditUsages",
                columns: new[] { "ResourceType", "ResourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PackageEnrollments_PatientId_Status",
                table: "PackageEnrollments",
                columns: new[] { "PatientId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PackageEnrollments_PaymentId",
                table: "PackageEnrollments",
                column: "PaymentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PackageEnrollments_ServicePackageId",
                table: "PackageEnrollments",
                column: "ServicePackageId");

            migrationBuilder.AddForeignKey(
                name: "FK_Appointments_PackageEnrollments_PackageEnrollmentId",
                table: "Appointments",
                column: "PackageEnrollmentId",
                principalTable: "PackageEnrollments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ServicePackages_Users_NutritionistApprovedById",
                table: "ServicePackages",
                column: "NutritionistApprovedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Appointments_PackageEnrollments_PackageEnrollmentId",
                table: "Appointments");

            migrationBuilder.DropForeignKey(
                name: "FK_ServicePackages_Users_NutritionistApprovedById",
                table: "ServicePackages");

            migrationBuilder.DropTable(
                name: "CancellationRefundInstructions");

            migrationBuilder.DropTable(
                name: "PackageCreditHistory");

            migrationBuilder.DropTable(
                name: "PackageCreditUsages");

            migrationBuilder.DropTable(
                name: "PackageEnrollments");

            migrationBuilder.DropIndex(
                name: "IX_ServicePackages_NutritionistApprovedById",
                table: "ServicePackages");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_PackageEnrollmentId",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "PackageServiceCode",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "ActivationRule",
                table: "ServicePackages");

            migrationBuilder.DropColumn(
                name: "CreditExpiryDays",
                table: "ServicePackages");

            migrationBuilder.DropColumn(
                name: "DietPlanCount",
                table: "ServicePackages");

            migrationBuilder.DropColumn(
                name: "DietPlanRevisionCount",
                table: "ServicePackages");

            migrationBuilder.DropColumn(
                name: "DurationDays",
                table: "ServicePackages");

            migrationBuilder.DropColumn(
                name: "EligibleServiceCodes",
                table: "ServicePackages");

            migrationBuilder.DropColumn(
                name: "FirstConsultationWithinDays",
                table: "ServicePackages");

            migrationBuilder.DropColumn(
                name: "FollowUpConsultationCount",
                table: "ServicePackages");

            migrationBuilder.DropColumn(
                name: "FollowUpConsultationMinutes",
                table: "ServicePackages");

            migrationBuilder.DropColumn(
                name: "InitialConsultationCount",
                table: "ServicePackages");

            migrationBuilder.DropColumn(
                name: "InitialConsultationMinutes",
                table: "ServicePackages");

            migrationBuilder.DropColumn(
                name: "NoShowCreditRule",
                table: "ServicePackages");

            migrationBuilder.DropColumn(
                name: "NutritionistApprovedAt",
                table: "ServicePackages");

            migrationBuilder.DropColumn(
                name: "NutritionistApprovedById",
                table: "ServicePackages");

            migrationBuilder.DropColumn(
                name: "NutritionistApprovedVersion",
                table: "ServicePackages");

            migrationBuilder.DropColumn(
                name: "ProgressTrackingFrequency",
                table: "ServicePackages");

            migrationBuilder.DropColumn(
                name: "RestoreCreditsOnProviderCancellation",
                table: "ServicePackages");

            migrationBuilder.DropColumn(
                name: "LastErrorCode",
                table: "PaymentRefunds");

            migrationBuilder.DropColumn(
                name: "ProcessingToken",
                table: "PaymentRefunds");

            migrationBuilder.DropColumn(
                name: "ProcessingUntil",
                table: "PaymentRefunds");

            migrationBuilder.DropColumn(
                name: "ProviderRequestStartedAt",
                table: "PaymentRefunds");

            migrationBuilder.DropColumn(
                name: "PackageNoShowCreditAction",
                table: "CancellationPolicies");

            migrationBuilder.DropColumn(
                name: "PackagePatientCancellationCreditAction",
                table: "CancellationPolicies");

            migrationBuilder.DropColumn(
                name: "BookingBufferMinutesSnapshot",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "PackageCreditKind",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "PackageEnrollmentId",
                table: "Appointments");
        }
    }
}
