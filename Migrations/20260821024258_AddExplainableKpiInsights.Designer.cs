using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using WorkManagementSystem.Infrastructure.Data;

#nullable disable

namespace WorkManagementSystem.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260821024258_AddExplainableKpiInsights")]
    partial class AddExplainableKpiInsights
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.20")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

            modelBuilder.HasSequence("EmployeeCodeSequence");

            modelBuilder.Entity("WorkManagementSystem.Domain.Entities.AuditLog", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("Action")
                        .IsRequired()
                        .HasMaxLength(64)
                        .HasColumnType("nvarchar(64)");

                    b.Property<Guid?>("ActorUserId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("DetailsJson")
                        .HasColumnType("nvarchar(max)");

                    b.Property<Guid>("EntityId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("EntityType")
                        .IsRequired()
                        .HasMaxLength(64)
                        .HasColumnType("nvarchar(64)");

                    b.Property<DateTime>("OccurredAt")
                        .HasColumnType("datetime2");

                    b.HasKey("Id");

                    b.HasIndex("ActorUserId", "OccurredAt");

                    b.HasIndex("EntityType", "EntityId", "OccurredAt");

                    b.ToTable("AuditLogs");
                });

            modelBuilder.Entity("WorkManagementSystem.Domain.Entities.CommentReaction", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid>("CommentId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("datetime2");

                    b.Property<string>("Emoji")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("Id");

                    b.HasIndex("CommentId", "UserId")
                        .IsUnique();

                    b.ToTable("CommentReactions");
                });

            modelBuilder.Entity("WorkManagementSystem.Domain.Entities.CommentSeen", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid>("CommentId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime>("SeenAt")
                        .HasColumnType("datetime2");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("Id");

                    b.HasIndex("CommentId", "UserId")
                        .IsUnique();

                    b.ToTable("CommentSeens");
                });

            modelBuilder.Entity("WorkManagementSystem.Domain.Entities.GeneratedTaskOccurrence", b =>
                {
                    b.Property<Guid>("TemplateId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime>("ScheduledForUtc")
                        .HasColumnType("datetime2");

                    b.Property<DateTime>("GeneratedAtUtc")
                        .HasColumnType("datetime2");

                    b.Property<Guid>("TaskId")
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("TemplateId", "ScheduledForUtc");

                    b.HasIndex("TaskId")
                        .IsUnique();

                    b.ToTable("GeneratedTaskOccurrences");
                });

            modelBuilder.Entity("WorkManagementSystem.Domain.Entities.KpiPeriod", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("datetime2");

                    b.Property<DateTime>("EndDate")
                        .HasColumnType("datetime2");

                    b.Property<DateTime?>("LockedAt")
                        .HasColumnType("datetime2");

                    b.Property<Guid?>("LockedBy")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<byte[]>("RowVersion")
                        .IsConcurrencyToken()
                        .IsRequired()
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("rowversion");

                    b.Property<DateTime>("StartDate")
                        .HasColumnType("datetime2");

                    b.Property<string>("Status")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Type")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("Id");

                    b.HasIndex("LockedBy");

                    b.HasIndex("StartDate", "EndDate")
                        .IsUnique();

                    b.ToTable("KpiPeriods", t =>
                        {
                            t.HasCheckConstraint("CK_KpiPeriods_Date_Range", "[EndDate] > [StartDate]");
                        });
                });

            modelBuilder.Entity("WorkManagementSystem.Domain.Entities.KpiResult", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<decimal>("ActualHours")
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)");

                    b.Property<int>("BonusPoints")
                        .HasColumnType("int");

                    b.Property<DateTime>("CalculatedAt")
                        .HasColumnType("datetime2");

                    b.Property<int>("CompletedLate")
                        .HasColumnType("int");

                    b.Property<int>("CompletedOnTime")
                        .HasColumnType("int");

                    b.Property<int>("CompletedTasks")
                        .HasColumnType("int");

                    b.Property<DateTime>("EffectiveFrom")
                        .HasColumnType("datetime2");

                    b.Property<DateTime>("EffectiveTo")
                        .HasColumnType("datetime2");

                    b.Property<string>("EmployeeCodeSnapshot")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("nvarchar(50)");

                    b.Property<string>("FormulaVersion")
                        .IsRequired()
                        .ValueGeneratedOnAdd()
                        .HasMaxLength(30)
                        .HasColumnType("nvarchar(30)")
                        .HasDefaultValue("1.0");

                    b.Property<string>("FullNameSnapshot")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("nvarchar(200)");

                    b.Property<bool>("IsAtRisk")
                        .HasColumnType("bit");

                    b.Property<bool>("IsManagerKpi")
                        .HasColumnType("bit");

                    b.Property<string>("Level")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime?>("LockedAt")
                        .HasColumnType("datetime2");

                    b.Property<int>("OverdueTasks")
                        .HasColumnType("int");

                    b.Property<int>("PenaltyPoints")
                        .HasColumnType("int");

                    b.Property<Guid>("PeriodId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<int>("PersonalScore")
                        .HasColumnType("int");

                    b.Property<decimal>("PlannedEffortHours")
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)");

                    b.Property<int>("ProgressReportCount")
                        .HasColumnType("int");

                    b.Property<int>("RejectedReports")
                        .HasColumnType("int");

                    b.Property<int>("ReviewPenaltyPoints")
                        .HasColumnType("int");

                    b.Property<string>("Role")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<int>("Score")
                        .HasColumnType("int");

                    b.Property<int>("TotalTasks")
                        .HasColumnType("int");

                    b.Property<double>("UnitAverageScore")
                        .HasColumnType("float");

                    b.Property<Guid?>("UnitId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("UnitNameSnapshot")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("nvarchar(200)");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("WarningMessage")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("Id");

                    b.HasIndex("UnitId");

                    b.HasIndex("UserId");

                    b.HasIndex("PeriodId", "UnitId");

                    b.HasIndex("PeriodId", "UserId")
                        .IsUnique();

                    b.ToTable("KpiResults", t =>
                        {
                            t.HasCheckConstraint("CK_KpiResults_Effective_Range", "[EffectiveTo] >= [EffectiveFrom]");

                            t.HasCheckConstraint("CK_KpiResults_FormulaVersion", "LEN([FormulaVersion]) > 0");

                            t.HasCheckConstraint("CK_KpiResults_Metric_Ranges", "[CompletedTasks] <= [TotalTasks] AND [CompletedOnTime] + [CompletedLate] <= [CompletedTasks] AND [OverdueTasks] <= [TotalTasks] AND [RejectedReports] <= [ProgressReportCount]");

                            t.HasCheckConstraint("CK_KpiResults_NonNegative", "[Score] >= 0 AND [TotalTasks] >= 0 AND [CompletedTasks] >= 0 AND [CompletedOnTime] >= 0 AND [CompletedLate] >= 0 AND [OverdueTasks] >= 0 AND [RejectedReports] >= 0 AND [ProgressReportCount] >= 0 AND [PlannedEffortHours] >= 0 AND [ActualHours] >= 0 AND [BonusPoints] >= 0 AND [PenaltyPoints] >= 0 AND [ReviewPenaltyPoints] >= 0 AND [UnitAverageScore] >= 0 AND [PersonalScore] >= 0");
                        });
                });

            modelBuilder.Entity("WorkManagementSystem.Domain.Entities.Notification", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("datetime2");

                    b.Property<bool>("IsRead")
                        .HasColumnType("bit");

                    b.Property<string>("Message")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("Id");

                    b.HasIndex("UserId");

                    b.ToTable("Notifications");
                });

            modelBuilder.Entity("WorkManagementSystem.Domain.Entities.Progress", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("Description")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<decimal>("HoursSpent")
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)");

                    b.Property<int>("Percent")
                        .HasColumnType("int");

                    b.Property<byte[]>("RowVersion")
                        .IsConcurrencyToken()
                        .IsRequired()
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("rowversion");

                    b.Property<int>("Status")
                        .HasColumnType("int");

                    b.Property<Guid>("TaskId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime>("UpdatedAt")
                        .HasColumnType("datetime2");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("Id");

                    b.HasIndex("TaskId", "UpdatedAt", "Id");

                    b.HasIndex("UserId", "Status", "UpdatedAt", "TaskId");

                    b.ToTable("Progresses", t =>
                        {
                            t.HasCheckConstraint("CK_Progress_HoursSpent_NonNegative", "[HoursSpent] >= 0");

                            t.HasCheckConstraint("CK_Progress_Percent_Range", "[Percent] >= 0 AND [Percent] <= 100");
                        });
                });

            modelBuilder.Entity("WorkManagementSystem.Domain.Entities.Project", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("datetime2");

                    b.Property<Guid>("CreatedBy")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("Description")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<bool>("IsArchived")
                        .HasColumnType("bit");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("nvarchar(450)");

                    b.Property<byte[]>("RowVersion")
                        .IsConcurrencyToken()
                        .IsRequired()
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("rowversion");

                    b.Property<Guid?>("UnitId")
                        .IsRequired()
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("Id");

                    b.HasIndex("CreatedBy");

                    b.HasIndex("UnitId", "Name")
                        .IsUnique();

                    b.ToTable("Projects");
                });

            modelBuilder.Entity("WorkManagementSystem.Domain.Entities.RecurringTaskAssignee", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid>("TemplateId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("Id");

                    b.HasIndex("TemplateId", "UserId")
                        .IsUnique();

                    b.HasIndex("UserId", "TemplateId");

                    b.ToTable("RecurringTaskAssignees");
                });

            modelBuilder.Entity("WorkManagementSystem.Domain.Entities.RecurringTaskTemplate", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime>("CreatedAtUtc")
                        .HasColumnType("datetime2");

                    b.Property<Guid>("CreatedByUserId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<int?>("DayOfMonth")
                        .HasColumnType("int");

                    b.Property<int?>("DayOfWeek")
                        .HasColumnType("int");

                    b.Property<string>("Description")
                        .IsRequired()
                        .HasMaxLength(1000)
                        .HasColumnType("nvarchar(1000)");

                    b.Property<int>("Interval")
                        .HasColumnType("int");

                    b.Property<bool>("IsActive")
                        .HasColumnType("bit");

                    b.Property<bool>("IsDeleted")
                        .HasColumnType("bit");

                    b.Property<DateTime?>("LastGeneratedAtUtc")
                        .HasColumnType("datetime2");

                    b.Property<DateTime>("NextRunAtUtc")
                        .HasColumnType("datetime2");

                    b.Property<decimal?>("PlannedEffortHours")
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)");

                    b.Property<int>("Priority")
                        .HasColumnType("int");

                    b.Property<Guid?>("ProjectId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<int>("RecurrenceType")
                        .HasColumnType("int");

                    b.Property<bool>("RequiresReview")
                        .HasColumnType("bit");

                    b.Property<byte[]>("RowVersion")
                        .IsConcurrencyToken()
                        .IsRequired()
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("rowversion");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("nvarchar(200)");

                    b.Property<Guid?>("UnitId")
                        .IsRequired()
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("Id");

                    b.HasIndex("CreatedByUserId");

                    b.HasIndex("UnitId");

                    b.HasIndex("IsActive", "NextRunAtUtc");

                    b.HasIndex("ProjectId", "UnitId");

                    b.ToTable("RecurringTaskTemplates", t =>
                        {
                            t.HasCheckConstraint("CK_RecurringTaskTemplates_Interval_Positive", "[Interval] >= 1");

                            t.HasCheckConstraint("CK_RecurringTaskTemplates_PlannedEffortHours_Positive", "[PlannedEffortHours] IS NULL OR [PlannedEffortHours] > 0");

                            t.HasCheckConstraint("CK_RecurringTaskTemplates_RecurrenceType_Range", "[RecurrenceType] >= 0 AND [RecurrenceType] <= 2");

                            t.HasCheckConstraint("CK_RecurringTaskTemplates_Schedule_Shape", "([RecurrenceType] = 0 AND [DayOfWeek] IS NULL AND [DayOfMonth] IS NULL) OR ([RecurrenceType] = 1 AND [DayOfWeek] BETWEEN 0 AND 6 AND [DayOfMonth] IS NULL) OR ([RecurrenceType] = 2 AND [DayOfWeek] IS NULL AND [DayOfMonth] BETWEEN 1 AND 31)");
                        });
                });

            modelBuilder.Entity("WorkManagementSystem.Domain.Entities.ReminderPolicy", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<int>("BeforeDueHours")
                        .HasColumnType("int");

                    b.Property<DateTime>("CreatedAtUtc")
                        .HasColumnType("datetime2");

                    b.Property<bool>("IsActive")
                        .HasColumnType("bit");

                    b.Property<bool>("NotifyAssignee")
                        .HasColumnType("bit");

                    b.Property<bool>("NotifyManager")
                        .HasColumnType("bit");

                    b.Property<int>("OverdueEscalationHours")
                        .HasColumnType("int");

                    b.Property<Guid?>("ProjectId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<byte[]>("RowVersion")
                        .IsConcurrencyToken()
                        .IsRequired()
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("rowversion");

                    b.Property<int>("ScopeType")
                        .HasColumnType("int");

                    b.Property<Guid?>("UnitId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime>("UpdatedAtUtc")
                        .HasColumnType("datetime2");

                    b.HasKey("Id");

                    b.HasIndex("ProjectId")
                        .IsUnique()
                        .HasFilter("[ScopeType] = 2 AND [ProjectId] IS NOT NULL");

                    b.HasIndex("ScopeType")
                        .IsUnique()
                        .HasFilter("[ScopeType] = 0");

                    b.HasIndex("UnitId")
                        .IsUnique()
                        .HasFilter("[ScopeType] = 1 AND [UnitId] IS NOT NULL");

                    b.ToTable("ReminderPolicies", t =>
                        {
                            t.HasCheckConstraint("CK_ReminderPolicies_BeforeDueHours_Range", "[BeforeDueHours] >= 1 AND [BeforeDueHours] <= 720");

                            t.HasCheckConstraint("CK_ReminderPolicies_OverdueEscalationHours_Range", "[OverdueEscalationHours] >= 0 AND [OverdueEscalationHours] <= 720");

                            t.HasCheckConstraint("CK_ReminderPolicies_ScopeType_Range", "[ScopeType] >= 0 AND [ScopeType] <= 2");

                            t.HasCheckConstraint("CK_ReminderPolicies_Scope_Shape", "([ScopeType] = 0 AND [UnitId] IS NULL AND [ProjectId] IS NULL) OR ([ScopeType] = 1 AND [UnitId] IS NOT NULL AND [ProjectId] IS NULL) OR ([ScopeType] = 2 AND [UnitId] IS NULL AND [ProjectId] IS NOT NULL)");
                        });
                });

            modelBuilder.Entity("WorkManagementSystem.Domain.Entities.ReportReview", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("Comment")
                        .HasColumnType("nvarchar(max)");

                    b.Property<bool>("IsApproved")
                        .HasColumnType("bit");

                    b.Property<Guid>("ProgressId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime>("ReviewedAt")
                        .HasColumnType("datetime2");

                    b.Property<Guid?>("ReviewerId")
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("Id");

                    b.HasIndex("ProgressId")
                        .IsUnique();

                    b.HasIndex("ReviewerId");

                    b.ToTable("Reviews");
                });

            modelBuilder.Entity("WorkManagementSystem.Domain.Entities.ScheduledNotification", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime>("CreatedAtUtc")
                        .HasColumnType("datetime2");

                    b.Property<string>("EventKey")
                        .IsRequired()
                        .HasMaxLength(160)
                        .HasColumnType("nvarchar(160)");

                    b.Property<string>("LastError")
                        .HasMaxLength(1000)
                        .HasColumnType("nvarchar(1000)");

                    b.Property<int>("RetryCount")
                        .HasColumnType("int");

                    b.Property<byte[]>("RowVersion")
                        .IsConcurrencyToken()
                        .IsRequired()
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("rowversion");

                    b.Property<DateTime>("ScheduledForUtc")
                        .HasColumnType("datetime2");

                    b.Property<DateTime?>("SentAtUtc")
                        .HasColumnType("datetime2");

                    b.Property<int>("Status")
                        .HasColumnType("int");

                    b.Property<Guid>("TaskId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<int>("Type")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    b.HasIndex("EventKey")
                        .IsUnique();

                    b.HasIndex("TaskId", "ScheduledForUtc");

                    b.HasIndex("TaskId", "Type")
                        .IsUnique();

                    b.HasIndex("Status", "ScheduledForUtc", "RetryCount");

                    b.HasIndex("TaskId", "SentAtUtc", "Id");

                    b.ToTable("ScheduledNotifications", t =>
                        {
                            t.HasCheckConstraint("CK_ScheduledNotifications_RetryCount_NonNegative", "[RetryCount] >= 0");

                            t.HasCheckConstraint("CK_ScheduledNotifications_Status_Range", "[Status] >= 0 AND [Status] <= 3");

                            t.HasCheckConstraint("CK_ScheduledNotifications_Type_Range", "[Type] >= 0 AND [Type] <= 2");
                        });
                });

            modelBuilder.Entity("WorkManagementSystem.Domain.Entities.SubTask", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("datetime2");

                    b.Property<bool>("IsCompleted")
                        .HasColumnType("bit");

                    b.Property<Guid>("TaskId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasColumnType("nvarchar(450)");

                    b.HasKey("Id");

                    b.HasIndex("TaskId", "Title")
                        .IsUnique();

                    b.ToTable("SubTasks");
                });

            modelBuilder.Entity("WorkManagementSystem.Domain.Entities.TaskAssignee", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid>("TaskId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid?>("UnitId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid?>("UserId")
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("Id");

                    b.HasIndex("TaskId", "UnitId")
                        .IsUnique()
                        .HasFilter("[UnitId] IS NOT NULL");

                    b.HasIndex("TaskId", "UserId")
                        .IsUnique()
                        .HasFilter("[UserId] IS NOT NULL");

                    b.HasIndex("UnitId", "TaskId");

                    b.HasIndex("UserId", "TaskId");

                    b.ToTable("TaskAssignees", t =>
                        {
                            t.HasCheckConstraint("CK_TaskAssignee_One_Target", "([UserId] IS NOT NULL AND [UnitId] IS NULL) OR ([UserId] IS NULL AND [UnitId] IS NOT NULL)");
                        });
                });

            modelBuilder.Entity("WorkManagementSystem.Domain.Entities.TaskComment", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("Content")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("datetime2");

                    b.Property<bool>("IsDeleted")
                        .HasColumnType("bit");

                    b.Property<Guid>("TaskId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("Id");

                    b.HasIndex("TaskId", "CreatedAt", "Id");

                    b.ToTable("TaskComments");
                });

            modelBuilder.Entity("WorkManagementSystem.Domain.Entities.TaskDependency", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("datetime2");

                    b.Property<Guid>("CreatedByUserId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid>("DependsOnTaskId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid>("TaskId")
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("Id");

                    b.HasIndex("CreatedByUserId");

                    b.HasIndex("DependsOnTaskId");

                    b.HasIndex("TaskId", "DependsOnTaskId")
                        .IsUnique();

                    b.ToTable("TaskDependencies", t =>
                        {
                            t.HasCheckConstraint("CK_TaskDependencies_NoSelfReference", "[TaskId] <> [DependsOnTaskId]");
                        });
                });

            modelBuilder.Entity("WorkManagementSystem.Domain.Entities.TaskHistory", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime>("ChangedAt")
                        .HasColumnType("datetime2");

                    b.Property<Guid>("ChangedBy")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("FieldName")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("NewValue")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("OldValue")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Reason")
                        .HasMaxLength(1000)
                        .HasColumnType("nvarchar(1000)");

                    b.Property<Guid?>("RelatedEntityId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid>("TaskId")
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("Id");

                    b.HasIndex("ChangedBy");

                    b.HasIndex("TaskId", "ChangedAt", "Id");

                    b.ToTable("TaskHistories");
                });

            modelBuilder.Entity("WorkManagementSystem.Domain.Entities.TaskItem", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<decimal>("ActualHours")
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)");

                    b.Property<DateTime?>("CompletedAt")
                        .HasColumnType("datetime2");

                    b.Property<Guid?>("CompletedBy")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("datetime2");

                    b.Property<Guid>("CreatedBy")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("Description")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime?>("DueDate")
                        .HasColumnType("datetime2");

                    b.Property<bool>("IsDeleted")
                        .HasColumnType("bit");

                    b.Property<decimal?>("PlannedEffortHours")
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)");

                    b.Property<int>("Priority")
                        .HasColumnType("int");

                    b.Property<Guid?>("ProjectId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<bool>("RequiresReview")
                        .HasColumnType("bit");

                    b.Property<byte[]>("RowVersion")
                        .IsConcurrencyToken()
                        .IsRequired()
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("rowversion");

                    b.Property<DateTime?>("StartDate")
                        .HasColumnType("datetime2");

                    b.Property<int>("Status")
                        .HasColumnType("int");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<Guid?>("UnitId")
                        .IsRequired()
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("Id");

                    b.HasIndex("CreatedBy");

                    b.HasIndex("UnitId");

                    b.HasIndex("ProjectId", "Status");

                    b.HasIndex("ProjectId", "UnitId");

                    b.ToTable("Tasks", t =>
                        {
                            t.HasCheckConstraint("CK_Tasks_ActualHours_NonNegative", "[ActualHours] >= 0");

                            t.HasCheckConstraint("CK_Tasks_Date_Range", "[StartDate] IS NULL OR [DueDate] IS NULL OR [DueDate] >= [StartDate]");

                            t.HasCheckConstraint("CK_Tasks_PlannedEffortHours_Positive", "[PlannedEffortHours] IS NULL OR [PlannedEffortHours] > 0");

                            t.HasCheckConstraint("CK_Tasks_Status_Range", "[Status] >= 0 AND [Status] <= 3");
                        });
                });

            modelBuilder.Entity("WorkManagementSystem.Domain.Entities.Unit", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<bool>("IsDeleted")
                        .HasColumnType("bit");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("nvarchar(450)");

                    b.Property<byte[]>("RowVersion")
                        .IsConcurrencyToken()
                        .IsRequired()
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("rowversion");

                    b.HasKey("Id");

                    b.HasIndex("Name")
                        .IsUnique();

                    b.ToTable("Units");
                });

            modelBuilder.Entity("WorkManagementSystem.Domain.Entities.UploadFile", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("datetime2");

                    b.Property<string>("FileName")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("nvarchar(200)");

                    b.Property<Guid?>("ProgressId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("StorageKey")
                        .IsRequired()
                        .HasMaxLength(255)
                        .HasColumnType("nvarchar(255)");

                    b.Property<Guid>("TaskId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid?>("UploadedBy")
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("Id");

                    b.HasIndex("UploadedBy");

                    b.HasIndex("ProgressId", "TaskId");

                    b.HasIndex("TaskId", "CreatedAt", "Id");

                    b.ToTable("UploadFiles");
                });

            modelBuilder.Entity("WorkManagementSystem.Domain.Entities.User", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("Email")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("EmployeeCode")
                        .IsRequired()
                        .HasColumnType("nvarchar(450)");

                    b.Property<string>("FullName")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<bool>("IsApproved")
                        .HasColumnType("bit");

                    b.Property<bool>("IsDeleted")
                        .HasColumnType("bit");

                    b.Property<DateTime>("JoinedUnitAt")
                        .HasColumnType("datetime2");

                    b.Property<string>("PasswordHash")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("PhoneNumber")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Role")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<byte[]>("RowVersion")
                        .IsConcurrencyToken()
                        .IsRequired()
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("rowversion");

                    b.Property<int>("TokenVersion")
                        .HasColumnType("int");

                    b.Property<Guid?>("UnitId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("Username")
                        .IsRequired()
                        .HasColumnType("nvarchar(450)");

                    b.HasKey("Id");

                    b.HasIndex("EmployeeCode")
                        .IsUnique();

                    b.HasIndex("UnitId");

                    b.HasIndex("Username")
                        .IsUnique();

                    b.ToTable("Users");
                });

            modelBuilder.Entity("WorkManagementSystem.Domain.Entities.UserCapacity", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid>("ChangedByUserId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("datetime2");

                    b.Property<DateTime>("EffectiveFrom")
                        .HasColumnType("datetime2");

                    b.Property<DateTime?>("EffectiveTo")
                        .HasColumnType("datetime2");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<decimal>("WeeklyCapacityHours")
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)");

                    b.HasKey("Id");

                    b.HasIndex("ChangedByUserId");

                    b.HasIndex("UserId")
                        .IsUnique()
                        .HasFilter("[EffectiveTo] IS NULL");

                    b.HasIndex("UserId", "EffectiveFrom");

                    b.ToTable("UserCapacities", t =>
                        {
                            t.HasCheckConstraint("CK_UserCapacities_Effective_Range", "[EffectiveTo] IS NULL OR [EffectiveTo] > [EffectiveFrom]");

                            t.HasCheckConstraint("CK_UserCapacities_WeeklyHours_Positive", "[WeeklyCapacityHours] > 0");
                        });
                });

            modelBuilder.Entity("WorkManagementSystem.Domain.Entities.UserUnit", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid>("UnitId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("Id");

                    b.HasIndex("UnitId");

                    b.HasIndex("UserId")
                        .IsUnique();

                    b.ToTable("UserUnits");
                });

            modelBuilder.Entity("WorkManagementSystem.Domain.Entities.UserWorkHistory", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("ChangeReason")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<Guid?>("ChangedBy")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("datetime2");

                    b.Property<DateTime>("EffectiveFrom")
                        .HasColumnType("datetime2");

                    b.Property<DateTime?>("EffectiveTo")
                        .HasColumnType("datetime2");

                    b.Property<string>("Role")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<Guid?>("UnitId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("Id");

                    b.HasIndex("ChangedBy");

                    b.HasIndex("UserId")
                        .IsUnique()
                        .HasFilter("[EffectiveTo] IS NULL");

                    b.HasIndex("UnitId", "EffectiveFrom");

                    b.HasIndex("UserId", "EffectiveFrom");

                    b.ToTable("UserWorkHistories");
                });

            modelBuilder.Entity("WorkManagementSystem.Domain.Entities.AuditLog", b =>
                {
                    b.HasOne("WorkManagementSystem.Domain.Entities.User", "ActorUser")
                        .WithMany()
                        .HasForeignKey("ActorUserId")
                        .OnDelete(DeleteBehavior.NoAction);

                    b.Navigation("ActorUser");
                });

            modelBuilder.Entity("WorkManagementSystem.Domain.Entities.GeneratedTaskOccurrence", b =>
                {
                    b.HasOne("WorkManagementSystem.Domain.Entities.TaskItem", "Task")
                        .WithMany()
                        .HasForeignKey("TaskId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.HasOne("WorkManagementSystem.Domain.Entities.RecurringTaskTemplate", "Template")
                        .WithMany("Occurrences")
                        .HasForeignKey("TemplateId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.Navigation("Task");

                    b.Navigation("Template");
                });

            modelBuilder.Entity("WorkManagementSystem.Domain.Entities.KpiPeriod", b =>
                {
                    b.HasOne("WorkManagementSystem.Domain.Entities.User", "Locker")
                        .WithMany()
                        .HasForeignKey("LockedBy")
                        .OnDelete(DeleteBehavior.NoAction);

                    b.Navigation("Locker");
                });

            modelBuilder.Entity("WorkManagementSystem.Domain.Entities.KpiResult", b =>
                {
                    b.HasOne("WorkManagementSystem.Domain.Entities.KpiPeriod", "Period")
                        .WithMany()
                        .HasForeignKey("PeriodId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.HasOne("WorkManagementSystem.Domain.Entities.Unit", "Unit")
                        .WithMany()
                        .HasForeignKey("UnitId")
                        .OnDelete(DeleteBehavior.NoAction);

                    b.HasOne("WorkManagementSystem.Domain.Entities.User", "User")
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.Navigation("Period");

                    b.Navigation("Unit");

                    b.Navigation("User");
                });

            modelBuilder.Entity("WorkManagementSystem.Domain.Entities.Notification", b =>
                {
                    b.HasOne("WorkManagementSystem.Domain.Entities.User", "User")
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("User");
                });

            modelBuilder.Entity("WorkManagementSystem.Domain.Entities.Progress", b =>
                {
                    b.HasOne("WorkManagementSystem.Domain.Entities.TaskItem", "Task")
                        .WithMany()
                        .HasForeignKey("TaskId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.HasOne("WorkManagementSystem.Domain.Entities.User", "User")
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.Navigation("Task");

                    b.Navigation("User");
                });

            modelBuilder.Entity("WorkManagementSystem.Domain.Entities.Project", b =>
                {
                    b.HasOne("WorkManagementSystem.Domain.Entities.User", "Creator")
                        .WithMany()
                        .HasForeignKey("CreatedBy")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.HasOne("WorkManagementSystem.Domain.Entities.Unit", "Unit")
                        .WithMany()
                        .HasForeignKey("UnitId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.Navigation("Creator");

                    b.Navigation("Unit");
                });

            modelBuilder.Entity("WorkManagementSystem.Domain.Entities.RecurringTaskAssignee", b =>
                {
                    b.HasOne("WorkManagementSystem.Domain.Entities.RecurringTaskTemplate", "Template")
                        .WithMany("Assignees")
                        .HasForeignKey("TemplateId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.HasOne("WorkManagementSystem.Domain.Entities.User", "User")
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.Navigation("Template");

                    b.Navigation("User");
                });

            modelBuilder.Entity("WorkManagementSystem.Domain.Entities.RecurringTaskTemplate", b =>
                {
                    b.HasOne("WorkManagementSystem.Domain.Entities.User", "CreatedByUser")
                        .WithMany()
                        .HasForeignKey("CreatedByUserId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.HasOne("WorkManagementSystem.Domain.Entities.Unit", "Unit")
                        .WithMany()
                        .HasForeignKey("UnitId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.HasOne("WorkManagementSystem.Domain.Entities.Project", "Project")
                        .WithMany()
                        .HasForeignKey("ProjectId", "UnitId")
                        .HasPrincipalKey("Id", "UnitId")
                        .OnDelete(DeleteBehavior.NoAction);

                    b.Navigation("CreatedByUser");

                    b.Navigation("Project");

                    b.Navigation("Unit");
                });

            modelBuilder.Entity("WorkManagementSystem.Domain.Entities.ReminderPolicy", b =>
                {
                    b.HasOne("WorkManagementSystem.Domain.Entities.Project", "Project")
                        .WithMany()
                        .HasForeignKey("ProjectId")
                        .OnDelete(DeleteBehavior.NoAction);

                    b.HasOne("WorkManagementSystem.Domain.Entities.Unit", "Unit")
                        .WithMany()
                        .HasForeignKey("UnitId")
                        .OnDelete(DeleteBehavior.NoAction);

                    b.Navigation("Project");

                    b.Navigation("Unit");
                });

            modelBuilder.Entity("WorkManagementSystem.Domain.Entities.ReportReview", b =>
                {
                    b.HasOne("WorkManagementSystem.Domain.Entities.Progress", "Progress")
                        .WithMany()
                        .HasForeignKey("ProgressId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.HasOne("WorkManagementSystem.Domain.Entities.User", "Reviewer")
                        .WithMany()
                        .HasForeignKey("ReviewerId")
                        .OnDelete(DeleteBehavior.NoAction);

                    b.Navigation("Progress");

                    b.Navigation("Reviewer");
                });

            modelBuilder.Entity("WorkManagementSystem.Domain.Entities.ScheduledNotification", b =>
                {
                    b.HasOne("WorkManagementSystem.Domain.Entities.TaskItem", "Task")
                        .WithMany()
                        .HasForeignKey("TaskId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.Navigation("Task");
                });

            modelBuilder.Entity("WorkManagementSystem.Domain.Entities.TaskAssignee", b =>
                {
                    b.HasOne("WorkManagementSystem.Domain.Entities.TaskItem", "Task")
                        .WithMany()
                        .HasForeignKey("TaskId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.HasOne("WorkManagementSystem.Domain.Entities.Unit", "Unit")
                        .WithMany()
                        .HasForeignKey("UnitId")
                        .OnDelete(DeleteBehavior.NoAction);

                    b.HasOne("WorkManagementSystem.Domain.Entities.User", "User")
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.NoAction);

                    b.Navigation("Task");

                    b.Navigation("Unit");

                    b.Navigation("User");
                });

            modelBuilder.Entity("WorkManagementSystem.Domain.Entities.TaskDependency", b =>
                {
                    b.HasOne("WorkManagementSystem.Domain.Entities.User", null)
                        .WithMany()
                        .HasForeignKey("CreatedByUserId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.HasOne("WorkManagementSystem.Domain.Entities.TaskItem", null)
                        .WithMany()
                        .HasForeignKey("DependsOnTaskId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.HasOne("WorkManagementSystem.Domain.Entities.TaskItem", null)
                        .WithMany()
                        .HasForeignKey("TaskId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();
                });

            modelBuilder.Entity("WorkManagementSystem.Domain.Entities.TaskHistory", b =>
                {
                    b.HasOne("WorkManagementSystem.Domain.Entities.User", null)
                        .WithMany()
                        .HasForeignKey("ChangedBy")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.HasOne("WorkManagementSystem.Domain.Entities.TaskItem", null)
                        .WithMany()
                        .HasForeignKey("TaskId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();
                });

            modelBuilder.Entity("WorkManagementSystem.Domain.Entities.TaskItem", b =>
                {
                    b.HasOne("WorkManagementSystem.Domain.Entities.User", "Creator")
                        .WithMany()
                        .HasForeignKey("CreatedBy")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.HasOne("WorkManagementSystem.Domain.Entities.Unit", "Unit")
                        .WithMany()
                        .HasForeignKey("UnitId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.HasOne("WorkManagementSystem.Domain.Entities.Project", "Project")
                        .WithMany()
                        .HasForeignKey("ProjectId", "UnitId")
                        .HasPrincipalKey("Id", "UnitId")
                        .OnDelete(DeleteBehavior.NoAction);

                    b.Navigation("Creator");

                    b.Navigation("Project");

                    b.Navigation("Unit");
                });

            modelBuilder.Entity("WorkManagementSystem.Domain.Entities.UploadFile", b =>
                {
                    b.HasOne("WorkManagementSystem.Domain.Entities.TaskItem", null)
                        .WithMany()
                        .HasForeignKey("TaskId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.HasOne("WorkManagementSystem.Domain.Entities.User", null)
                        .WithMany()
                        .HasForeignKey("UploadedBy")
                        .OnDelete(DeleteBehavior.NoAction);

                    b.HasOne("WorkManagementSystem.Domain.Entities.Progress", null)
                        .WithMany()
                        .HasForeignKey("ProgressId", "TaskId")
                        .HasPrincipalKey("Id", "TaskId")
                        .OnDelete(DeleteBehavior.NoAction);
                });

            modelBuilder.Entity("WorkManagementSystem.Domain.Entities.User", b =>
                {
                    b.HasOne("WorkManagementSystem.Domain.Entities.Unit", "Unit")
                        .WithMany()
                        .HasForeignKey("UnitId")
                        .OnDelete(DeleteBehavior.NoAction);

                    b.Navigation("Unit");
                });

            modelBuilder.Entity("WorkManagementSystem.Domain.Entities.UserCapacity", b =>
                {
                    b.HasOne("WorkManagementSystem.Domain.Entities.User", "ChangedByUser")
                        .WithMany()
                        .HasForeignKey("ChangedByUserId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.HasOne("WorkManagementSystem.Domain.Entities.User", "User")
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.Navigation("ChangedByUser");

                    b.Navigation("User");
                });

            modelBuilder.Entity("WorkManagementSystem.Domain.Entities.UserUnit", b =>
                {
                    b.HasOne("WorkManagementSystem.Domain.Entities.Unit", "Unit")
                        .WithMany()
                        .HasForeignKey("UnitId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("WorkManagementSystem.Domain.Entities.User", "User")
                        .WithMany("UserUnits")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Unit");

                    b.Navigation("User");
                });

            modelBuilder.Entity("WorkManagementSystem.Domain.Entities.UserWorkHistory", b =>
                {
                    b.HasOne("WorkManagementSystem.Domain.Entities.User", "ChangedByUser")
                        .WithMany()
                        .HasForeignKey("ChangedBy")
                        .OnDelete(DeleteBehavior.NoAction);

                    b.HasOne("WorkManagementSystem.Domain.Entities.Unit", "Unit")
                        .WithMany()
                        .HasForeignKey("UnitId")
                        .OnDelete(DeleteBehavior.NoAction);

                    b.HasOne("WorkManagementSystem.Domain.Entities.User", "User")
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.Navigation("ChangedByUser");

                    b.Navigation("Unit");

                    b.Navigation("User");
                });

            modelBuilder.Entity("WorkManagementSystem.Domain.Entities.RecurringTaskTemplate", b =>
                {
                    b.Navigation("Assignees");

                    b.Navigation("Occurrences");
                });

            modelBuilder.Entity("WorkManagementSystem.Domain.Entities.User", b =>
                {
                    b.Navigation("UserUnits");
                });
#pragma warning restore 612, 618
        }
    }
}
