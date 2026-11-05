#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;

namespace OrgChat.Infrastructure.Migrations;

public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Users",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                Username = table.Column<string>(type: "TEXT(100)", maxLength: 100, nullable: false),
                Email = table.Column<string>(type: "TEXT(256)", maxLength: 256, nullable: false),
                DisplayName = table.Column<string>(type: "TEXT(100)", maxLength: 100, nullable: false),
                PasswordHash = table.Column<string>(type: "TEXT", nullable: false),
                Role = table.Column<byte>(type: "INTEGER", nullable: false),
                IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Users", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Users_Email",
            table: "Users",
            column: "Email",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Users_Username",
            table: "Users",
            column: "Username",
            unique: true);

        migrationBuilder.CreateTable(
            name: "Conversations",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                Title = table.Column<string>(type: "TEXT(200)", maxLength: 200, nullable: true),
                Type = table.Column<byte>(type: "INTEGER", nullable: false),
                CreatedById = table.Column<int>(type: "INTEGER", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Conversations", x => x.Id);
                table.ForeignKey(
                    name: "FK_Conversations_Users_CreatedById",
                    column: x => x.CreatedById,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Conversations_CreatedById",
            table: "Conversations",
            column: "CreatedById");

        migrationBuilder.CreateTable(
            name: "ConversationParticipants",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                ConversationId = table.Column<int>(type: "INTEGER", nullable: false),
                UserId = table.Column<int>(type: "INTEGER", nullable: false),
                JoinedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                LastReadAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                IsGroupAdmin = table.Column<bool>(type: "INTEGER", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ConversationParticipants", x => x.Id);
                table.ForeignKey(
                    name: "FK_ConversationParticipants_Conversations_ConversationId",
                    column: x => x.ConversationId,
                    principalTable: "Conversations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_ConversationParticipants_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ConversationParticipants_ConversationId_UserId",
            table: "ConversationParticipants",
            columns: new[] { "ConversationId", "UserId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ConversationParticipants_UserId",
            table: "ConversationParticipants",
            column: "UserId");

        migrationBuilder.CreateTable(
            name: "Messages",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                ConversationId = table.Column<int>(type: "INTEGER", nullable: false),
                SenderId = table.Column<int>(type: "INTEGER", nullable: false),
                Content = table.Column<string>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                EditedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Messages", x => x.Id);
                table.ForeignKey(
                    name: "FK_Messages_Conversations_ConversationId",
                    column: x => x.ConversationId,
                    principalTable: "Conversations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_Messages_Users_SenderId",
                    column: x => x.SenderId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Messages_ConversationId_CreatedAt",
            table: "Messages",
            columns: new[] { "ConversationId", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_Messages_SenderId",
            table: "Messages",
            column: "SenderId");

        migrationBuilder.CreateTable(
            name: "Notifications",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                UserId = table.Column<int>(type: "INTEGER", nullable: false),
                Type = table.Column<string>(type: "TEXT(100)", maxLength: 100, nullable: false),
                Payload = table.Column<string>(type: "TEXT", nullable: false),
                ReadAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Notifications", x => x.Id);
                table.ForeignKey(
                    name: "FK_Notifications_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Notifications_UserId",
            table: "Notifications",
            column: "UserId");

        migrationBuilder.CreateTable(
            name: "Attachments",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                MessageId = table.Column<int>(type: "INTEGER", nullable: false),
                OriginalFileName = table.Column<string>(type: "TEXT(500)", maxLength: 500, nullable: false),
                StorageFileName = table.Column<string>(type: "TEXT(200)", maxLength: 200, nullable: false),
                ContentType = table.Column<string>(type: "TEXT(100)", maxLength: 100, nullable: false),
                SizeInBytes = table.Column<long>(type: "INTEGER", nullable: false),
                StoragePath = table.Column<string>(type: "TEXT(1000)", maxLength: 1000, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Attachments", x => x.Id);
                table.ForeignKey(
                    name: "FK_Attachments_Messages_MessageId",
                    column: x => x.MessageId,
                    principalTable: "Messages",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Attachments_MessageId",
            table: "Attachments",
            column: "MessageId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "Attachments");
        migrationBuilder.DropTable(name: "Notifications");
        migrationBuilder.DropTable(name: "Messages");
        migrationBuilder.DropTable(name: "ConversationParticipants");
        migrationBuilder.DropTable(name: "Conversations");
        migrationBuilder.DropTable(name: "Users");
    }
}
