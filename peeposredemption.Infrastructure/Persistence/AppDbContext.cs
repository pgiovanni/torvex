using Microsoft.EntityFrameworkCore;
using peeposredemption.Domain.Entities;
using peeposredemption.Domain.Entities.Games;
using System;
using System.Collections.Generic;
using System.Text;

namespace peeposredemption.Infrastructure.Persistence
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
        public DbSet<User> Users { get; set; }
        public DbSet<Server> Servers { get; set; }
        public DbSet<ServerMember> ServerMembers { get; set; }
        public DbSet<Channel> Channels { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<DirectMessage> DirectMessages { get; set; }
        public DbSet<ServerInvite> ServerInvites { get; set; }
        public DbSet<FriendRequest> FriendRequests { get; set; }
        public DbSet<BannedMember> BannedMembers { get; set; }
        public DbSet<ModerationLog> ModerationLogs { get; set; }
        public DbSet<ServerEmoji> ServerEmojis { get; set; }
        public DbSet<StorageUpgradePurchase> StorageUpgradePurchases { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<ReferralCode> ReferralCodes { get; set; }
        public DbSet<ReferralPurchase> ReferralPurchases { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<OrbTransaction> OrbTransactions { get; set; }
        public DbSet<UserLoginStreak> UserLoginStreaks { get; set; }
        public DbSet<OrbPurchase> OrbPurchases { get; set; }
        public DbSet<OrbGift> OrbGifts { get; set; }
        public DbSet<ParentalLink> ParentalLinks { get; set; }
        public DbSet<BadgeDefinition> BadgeDefinitions { get; set; }
        public DbSet<UserBadge> UserBadges { get; set; }
        public DbSet<UserActivityStats> UserActivityStats { get; set; }
        public DbSet<Artist> Artists { get; set; }
        public DbSet<ArtItem> ArtItems { get; set; }
        public DbSet<ArtistCommission> ArtistCommissions { get; set; }
        public DbSet<ArtistPayout> ArtistPayouts { get; set; }
        public DbSet<ArtistSubmission> ArtistSubmissions { get; set; }
        public DbSet<PeepoSubmission> PeepoSubmissions { get; set; }

        // Guild config
        public DbSet<GuildConfig> GuildConfigs { get; set; }

        // Game system
        public DbSet<PlayerCharacter> PlayerCharacters { get; set; }
        public DbSet<ItemDefinition> ItemDefinitions { get; set; }
        public DbSet<PlayerInventoryItem> PlayerInventoryItems { get; set; }
        public DbSet<MonsterDefinition> MonsterDefinitions { get; set; }
        public DbSet<MonsterLootEntry> MonsterLootEntries { get; set; }
        public DbSet<CombatSession> CombatSessions { get; set; }
        public DbSet<PlayerSkill> PlayerSkills { get; set; }
        public DbSet<GameChannelConfig> GameChannelConfigs { get; set; }
        public DbSet<TradeOffer> TradeOffers { get; set; }
        public DbSet<CraftingRecipe> CraftingRecipes { get; set; }
        public DbSet<CraftingRecipeIngredient> CraftingRecipeIngredients { get; set; }
        public DbSet<MarketplaceListing> MarketplaceListings { get; set; }
        public DbSet<CoinTransaction> CoinTransactions { get; set; }
        public DbSet<ItemTransaction> ItemTransactions { get; set; }

        // Voice sessions
        public DbSet<VoiceSession> VoiceSessions { get; set; }

        // Feature requests
        public DbSet<FeatureRequest> FeatureRequests { get; set; }

        // Support tickets
        public DbSet<SupportTicket> SupportTickets { get; set; }

        // Sales leads (contact form)
        public DbSet<Lead> Leads { get; set; }

        // Customer contact/billing profiles
        public DbSet<CustomerProfile> CustomerProfiles { get; set; }
        public DbSet<PackageOrder> PackageOrders { get; set; }

        // ── Games hub (chess / connect4 / tictactoe / wordle) ──
        public DbSet<GameRating> GameRatings { get; set; }
        public DbSet<GameMatch> GameMatches { get; set; }
        public DbSet<WordlePlay> WordlePlays { get; set; }

        // Anti-alt security
        public DbSet<IpBan> IpBans { get; set; }
        public DbSet<UserDevice> UserDevices { get; set; }
        public DbSet<UserIpLog> UserIpLogs { get; set; }
        public DbSet<UserFingerprint> UserFingerprints { get; set; }
        public DbSet<BannedFingerprint> BannedFingerprints { get; set; }
        public DbSet<AltSuspicion> AltSuspicions { get; set; }

        // Torvex Gold
        public DbSet<TorvexGoldSubscription> GoldSubscriptions { get; set; }

        // Message attachments
        public DbSet<MessageAttachment> MessageAttachments { get; set; }

        // Discord bot integration
        public DbSet<DiscordLink> DiscordLinks { get; set; }

        // Peepo rarity voting
        public DbSet<PeepoRarityVote> PeepoRarityVotes { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Force snake_case so Windows dev and Linux prod behave identically
            foreach (var entity in modelBuilder.Model.GetEntityTypes())
            {
                entity.SetTableName(ToSnakeCase(entity.GetTableName()!));
                foreach (var prop in entity.GetProperties())
                    prop.SetColumnName(ToSnakeCase(prop.GetColumnName()!));
                foreach (var key in entity.GetKeys())
                    key.SetName(ToSnakeCase(key.GetName()!));
                foreach (var fk in entity.GetForeignKeys())
                    fk.SetConstraintName(ToSnakeCase(fk.GetConstraintName()!));
            }

            // GuildConfig — one row per Discord guild
            modelBuilder.Entity<GuildConfig>()
                .HasIndex(g => g.GuildId)
                .IsUnique();

            // Unique constraints
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username)
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            // Composite key for join table
            modelBuilder.Entity<ServerMember>()
                .HasKey(sm => new { sm.UserId, sm.ServerId });

            // Map ServerInvite.CreatedByUserId as the FK for the CreatedBy nav property
            modelBuilder.Entity<ServerInvite>()
                .HasOne(si => si.CreatedBy)
                .WithMany()
                .HasForeignKey(si => si.CreatedByUserId);

            // VoiceSession
            modelBuilder.Entity<VoiceSession>()
                .HasOne(v => v.User)
                .WithMany()
                .HasForeignKey(v => v.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<VoiceSession>()
                .HasIndex(v => new { v.UserId, v.LeftAt });

            // Message reply self-reference
            modelBuilder.Entity<Message>()
                .HasOne(m => m.ReplyToMessage)
                .WithMany()
                .HasForeignKey(m => m.ReplyToMessageId)
                .OnDelete(DeleteBehavior.SetNull);

            // Prevent cascade delete conflict on DirectMessages
            modelBuilder.Entity<DirectMessage>()
                .HasOne(dm => dm.Sender)
                .WithMany(u => u.SentDirectMessages)
                .HasForeignKey(dm => dm.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<DirectMessage>()
                .HasOne(dm => dm.Recipient)
                .WithMany(u => u.ReceivedDirectMessages)
                .HasForeignKey(dm => dm.RecipientId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<FriendRequest>()
                .HasOne(fr => fr.Sender)
                .WithMany()
                .HasForeignKey(fr => fr.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<FriendRequest>()
                .HasOne(fr => fr.Receiver)
                .WithMany()
                .HasForeignKey(fr => fr.ReceiverId)
                .OnDelete(DeleteBehavior.Restrict);

            // Prevent cascade delete conflict on BannedMember (multiple FKs to Users)
            modelBuilder.Entity<BannedMember>()
                .HasOne(b => b.BannedBy)
                .WithMany()
                .HasForeignKey(b => b.BannedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Prevent cascade delete conflict on ModerationLog (multiple FKs to Users)
            modelBuilder.Entity<ModerationLog>()
                .HasOne(ml => ml.Moderator)
                .WithMany()
                .HasForeignKey(ml => ml.ModeratorId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ModerationLog>()
                .HasOne(ml => ml.TargetUser)
                .WithMany()
                .HasForeignKey(ml => ml.TargetUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // ServerEmoji: unique (ServerId, Name)
            modelBuilder.Entity<ServerEmoji>()
                .HasIndex(e => new { e.ServerId, e.Name })
                .IsUnique();

            modelBuilder.Entity<ServerEmoji>()
                .HasOne(e => e.UploadedBy)
                .WithMany()
                .HasForeignKey(e => e.UploadedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StorageUpgradePurchase>()
                .HasOne(p => p.Server)
                .WithMany()
                .HasForeignKey(p => p.ServerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Notification>()
                .HasOne(n => n.User)
                .WithMany()
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Notification>()
                .HasOne(n => n.FromUser)
                .WithMany()
                .HasForeignKey(n => n.FromUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ReferralCode>()
                .HasOne(r => r.Owner)
                .WithMany()
                .HasForeignKey(r => r.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ReferralPurchase>()
                .HasOne(p => p.Purchaser)
                .WithMany()
                .HasForeignKey(p => p.PurchaserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ReferralPurchase>()
                .HasOne(p => p.ReferralCode)
                .WithMany(r => r.Purchases)
                .HasForeignKey(p => p.ReferralCodeId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<RefreshToken>()
                .HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<RefreshToken>()
                .HasIndex(r => r.Token)
                .IsUnique();

            // OrbTransaction
            modelBuilder.Entity<OrbTransaction>()
                .HasOne(t => t.User)
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<OrbTransaction>()
                .HasIndex(t => new { t.UserId, t.CreatedAt });

            // UserLoginStreak — one per user
            modelBuilder.Entity<UserLoginStreak>()
                .HasOne(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserLoginStreak>()
                .HasIndex(s => s.UserId)
                .IsUnique();

            // OrbGift — multiple FKs to Users
            modelBuilder.Entity<OrbGift>()
                .HasOne(g => g.Sender)
                .WithMany()
                .HasForeignKey(g => g.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<OrbGift>()
                .HasOne(g => g.Recipient)
                .WithMany()
                .HasForeignKey(g => g.RecipientId)
                .OnDelete(DeleteBehavior.Restrict);

            // ParentalLink
            modelBuilder.Entity<ParentalLink>()
                .HasIndex(l => l.LinkCode)
                .IsUnique();

            modelBuilder.Entity<ParentalLink>()
                .HasOne(l => l.Parent)
                .WithMany(u => u.ParentalLinksAsParent)
                .HasForeignKey(l => l.ParentUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ParentalLink>()
                .HasOne(l => l.Child)
                .WithMany(u => u.ParentalLinksAsChild)
                .HasForeignKey(l => l.ChildUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // OrbPurchase
            modelBuilder.Entity<OrbPurchase>()
                .HasOne(p => p.User)
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // BadgeDefinition
            modelBuilder.Entity<BadgeDefinition>()
                .HasIndex(b => new { b.StatKey, b.Threshold });

            // UserBadge — unique (UserId, BadgeDefinitionId)
            modelBuilder.Entity<UserBadge>()
                .HasIndex(ub => new { ub.UserId, ub.BadgeDefinitionId })
                .IsUnique();

            modelBuilder.Entity<UserBadge>()
                .HasOne(ub => ub.User)
                .WithMany()
                .HasForeignKey(ub => ub.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserBadge>()
                .HasOne(ub => ub.BadgeDefinition)
                .WithMany()
                .HasForeignKey(ub => ub.BadgeDefinitionId)
                .OnDelete(DeleteBehavior.Restrict);

            // UserActivityStats — one per user
            modelBuilder.Entity<UserActivityStats>()
                .HasOne(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserActivityStats>()
                .HasIndex(s => s.UserId)
                .IsUnique();

            // Artist — unique payout email
            modelBuilder.Entity<Artist>()
                .HasIndex(a => a.PayoutEmail)
                .IsUnique();

            modelBuilder.Entity<Artist>()
                .HasOne(a => a.User)
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // ArtItem
            modelBuilder.Entity<ArtItem>()
                .HasOne(i => i.Artist)
                .WithMany()
                .HasForeignKey(i => i.ArtistId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ArtItem>()
                .HasIndex(i => new { i.ArtistId, i.Rarity });

            modelBuilder.Entity<ArtItem>()
                .HasIndex(i => i.R2Key)
                .IsUnique();

            // ArtistCommission — immutable ledger
            modelBuilder.Entity<ArtistCommission>()
                .HasOne(c => c.Artist)
                .WithMany()
                .HasForeignKey(c => c.ArtistId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ArtistCommission>()
                .HasOne(c => c.ArtItem)
                .WithMany()
                .HasForeignKey(c => c.ArtItemId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ArtistCommission>()
                .HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ArtistCommission>()
                .HasIndex(c => new { c.ArtistId, c.CreatedAt });

            modelBuilder.Entity<ArtistCommission>()
                .HasIndex(c => c.ArtItemId);

            // ArtistPayout
            modelBuilder.Entity<ArtistPayout>()
                .HasOne(p => p.Artist)
                .WithMany()
                .HasForeignKey(p => p.ArtistId)
                .OnDelete(DeleteBehavior.Restrict);

            // ArtistSubmission
            modelBuilder.Entity<ArtistSubmission>()
                .HasOne(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ArtistSubmission>()
                .HasIndex(s => new { s.UserId, s.Status });

            // PeepoSubmission
            modelBuilder.Entity<PeepoSubmission>()
                .HasIndex(p => p.Status);

            // ── Game System ────────────────────────────────────────────

            // PlayerCharacter — one per user
            modelBuilder.Entity<PlayerCharacter>()
                .HasOne(p => p.User)
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PlayerCharacter>()
                .HasIndex(p => p.UserId)
                .IsUnique();

            // ItemDefinition — unique name
            modelBuilder.Entity<ItemDefinition>()
                .HasIndex(i => i.Name)
                .IsUnique();

            // PlayerInventoryItem
            modelBuilder.Entity<PlayerInventoryItem>()
                .HasOne(i => i.Player)
                .WithMany(p => p.Inventory)
                .HasForeignKey(i => i.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PlayerInventoryItem>()
                .HasOne(i => i.ItemDefinition)
                .WithMany()
                .HasForeignKey(i => i.ItemDefinitionId)
                .OnDelete(DeleteBehavior.Restrict);

            // MonsterLootEntry
            modelBuilder.Entity<MonsterLootEntry>()
                .HasOne(l => l.MonsterDefinition)
                .WithMany(m => m.LootTable)
                .HasForeignKey(l => l.MonsterDefinitionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<MonsterLootEntry>()
                .HasOne(l => l.ItemDefinition)
                .WithMany()
                .HasForeignKey(l => l.ItemDefinitionId)
                .OnDelete(DeleteBehavior.Restrict);

            // CombatSession
            modelBuilder.Entity<CombatSession>()
                .HasOne(c => c.Player)
                .WithMany(p => p.CombatSessions)
                .HasForeignKey(c => c.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CombatSession>()
                .HasOne(c => c.MonsterDefinition)
                .WithMany()
                .HasForeignKey(c => c.MonsterDefinitionId)
                .OnDelete(DeleteBehavior.Restrict);

            // PlayerSkill — unique (PlayerId, SkillType)
            modelBuilder.Entity<PlayerSkill>()
                .HasOne(s => s.Player)
                .WithMany(p => p.Skills)
                .HasForeignKey(s => s.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PlayerSkill>()
                .HasIndex(s => new { s.PlayerId, s.SkillType })
                .IsUnique();

            // GameChannelConfig — unique per channel
            modelBuilder.Entity<GameChannelConfig>()
                .HasOne(g => g.Channel)
                .WithMany()
                .HasForeignKey(g => g.ChannelId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<GameChannelConfig>()
                .HasIndex(g => g.ChannelId)
                .IsUnique();

            // TradeOffer
            modelBuilder.Entity<TradeOffer>()
                .HasOne(t => t.Initiator)
                .WithMany()
                .HasForeignKey(t => t.InitiatorId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TradeOffer>()
                .HasOne(t => t.Recipient)
                .WithMany()
                .HasForeignKey(t => t.RecipientId)
                .OnDelete(DeleteBehavior.Restrict);

            // CraftingRecipe
            modelBuilder.Entity<CraftingRecipe>()
                .HasOne(r => r.OutputItem)
                .WithMany()
                .HasForeignKey(r => r.OutputItemId)
                .OnDelete(DeleteBehavior.Restrict);

            // CraftingRecipeIngredient
            modelBuilder.Entity<CraftingRecipeIngredient>()
                .HasOne(i => i.Recipe)
                .WithMany(r => r.Ingredients)
                .HasForeignKey(i => i.RecipeId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CraftingRecipeIngredient>()
                .HasOne(i => i.ItemDefinition)
                .WithMany()
                .HasForeignKey(i => i.ItemDefinitionId)
                .OnDelete(DeleteBehavior.Restrict);

            // MarketplaceListing
            modelBuilder.Entity<MarketplaceListing>()
                .HasOne(l => l.Seller)
                .WithMany()
                .HasForeignKey(l => l.SellerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<MarketplaceListing>()
                .HasOne(l => l.Buyer)
                .WithMany()
                .HasForeignKey(l => l.BuyerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<MarketplaceListing>()
                .HasOne(l => l.ItemDefinition)
                .WithMany()
                .HasForeignKey(l => l.ItemDefinitionId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<MarketplaceListing>()
                .HasIndex(l => new { l.Status, l.ExpiresAt });

            // ── Feature Requests ────────────────────────────────────────
            modelBuilder.Entity<FeatureRequest>()
                .HasOne(f => f.User)
                .WithMany()
                .HasForeignKey(f => f.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<FeatureRequest>()
                .HasIndex(f => f.UserId);

            // ── Support Tickets ────────────────────────────────────────
            modelBuilder.Entity<SupportTicket>()
                .HasOne(t => t.User)
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SupportTicket>()
                .HasIndex(t => t.UserId);

            // ── Sales Leads ────────────────────────────────────────────
            modelBuilder.Entity<Lead>()
                .HasIndex(l => new { l.Status, l.CreatedAt });

            // ── Customer Profiles (one-to-one with User) ───────────────
            modelBuilder.Entity<CustomerProfile>()
                .HasKey(p => p.UserId);

            modelBuilder.Entity<CustomerProfile>()
                .HasOne(p => p.User)
                .WithOne()
                .HasForeignKey<CustomerProfile>(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // ── Package orders (Stripe Checkout) ───────────────────────
            modelBuilder.Entity<PackageOrder>()
                .HasIndex(o => o.StripeSessionId)
                .IsUnique();
            modelBuilder.Entity<PackageOrder>()
                .HasIndex(o => new { o.UserId, o.CreatedAt });
            modelBuilder.Entity<PackageOrder>()
                .HasIndex(o => new { o.PackageSlug, o.Status, o.FulfilledAt });
            modelBuilder.Entity<PackageOrder>()
                .HasOne(o => o.User)
                .WithMany()
                .HasForeignKey(o => o.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // ── Games hub ───────────────────────────────────────────────
            modelBuilder.Entity<GameRating>()
                .HasIndex(r => new { r.UserId, r.Game })
                .IsUnique();
            modelBuilder.Entity<GameRating>()
                .HasIndex(r => new { r.Game, r.Rating });
            modelBuilder.Entity<GameRating>()
                .HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<GameRating>()
                .Property(r => r.Game).HasMaxLength(32);

            modelBuilder.Entity<GameMatch>()
                .HasIndex(m => new { m.Game, m.Status, m.CreatedAt });
            modelBuilder.Entity<GameMatch>()
                .HasIndex(m => new { m.Player1Id, m.Status });
            modelBuilder.Entity<GameMatch>()
                .HasIndex(m => new { m.Player2Id, m.Status });
            modelBuilder.Entity<GameMatch>()
                .HasOne(m => m.Player1)
                .WithMany()
                .HasForeignKey(m => m.Player1Id)
                .OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<GameMatch>()
                .HasOne(m => m.Player2)
                .WithMany()
                .HasForeignKey(m => m.Player2Id)
                .OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<GameMatch>()
                .Property(m => m.Game).HasMaxLength(32);
            modelBuilder.Entity<GameMatch>()
                .Property(m => m.CreatorSeat).HasMaxLength(8);
            modelBuilder.Entity<GameMatch>()
                .Property(m => m.Difficulty).HasMaxLength(8);
            modelBuilder.Entity<GameMatch>()
                .Property(m => m.StateJson).HasColumnType("jsonb");

            modelBuilder.Entity<WordlePlay>()
                .HasIndex(w => new { w.UserId, w.PuzzleDate })
                .IsUnique()
                .HasFilter("puzzle_date IS NOT NULL");
            modelBuilder.Entity<WordlePlay>()
                .HasIndex(w => new { w.PuzzleDate, w.Finished });
            modelBuilder.Entity<WordlePlay>()
                .HasOne(w => w.User)
                .WithMany()
                .HasForeignKey(w => w.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<WordlePlay>()
                .Property(w => w.Answer).HasMaxLength(8);

            // ── Anti-Alt Security ────────────────────────────────────────

            // IpBan — unique IP
            modelBuilder.Entity<IpBan>()
                .HasIndex(b => b.IpAddress)
                .IsUnique();

            modelBuilder.Entity<IpBan>()
                .HasOne(b => b.BannedBy)
                .WithMany()
                .HasForeignKey(b => b.BannedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // UserDevice — unique (DeviceId, UserId)
            modelBuilder.Entity<UserDevice>()
                .HasIndex(d => new { d.DeviceId, d.UserId })
                .IsUnique();

            modelBuilder.Entity<UserDevice>()
                .HasIndex(d => d.DeviceId);

            modelBuilder.Entity<UserDevice>()
                .HasOne(d => d.User)
                .WithMany()
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // UserIpLog
            modelBuilder.Entity<UserIpLog>()
                .HasIndex(l => l.IpAddress);

            modelBuilder.Entity<UserIpLog>()
                .HasIndex(l => new { l.UserId, l.SeenAt });

            modelBuilder.Entity<UserIpLog>()
                .HasOne(l => l.User)
                .WithMany()
                .HasForeignKey(l => l.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // UserFingerprint
            modelBuilder.Entity<UserFingerprint>()
                .HasIndex(f => f.FingerprintHash);

            modelBuilder.Entity<UserFingerprint>()
                .HasOne(f => f.User)
                .WithMany()
                .HasForeignKey(f => f.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // BannedFingerprint — unique hash prevents race condition duplicates
            modelBuilder.Entity<BannedFingerprint>()
                .HasIndex(b => b.FingerprintHash)
                .IsUnique();

            modelBuilder.Entity<BannedFingerprint>()
                .HasOne(b => b.BannedBy)
                .WithMany()
                .HasForeignKey(b => b.BannedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // ── Behavioral Analysis ────────────────────────────────────────

            // Performance indexes for behavioral queries
            modelBuilder.Entity<DirectMessage>()
                .HasIndex(dm => new { dm.SenderId, dm.SentAt });

            modelBuilder.Entity<DirectMessage>()
                .HasIndex(dm => new { dm.RecipientId, dm.SentAt });

            modelBuilder.Entity<Message>()
                .HasIndex(m => new { m.AuthorId, m.SentAt });

            modelBuilder.Entity<Message>()
                .HasIndex(m => new { m.ChannelId, m.SentAt });

            modelBuilder.Entity<DirectMessage>()
                .HasIndex(dm => new { dm.RecipientId, dm.IsRead });

            modelBuilder.Entity<Notification>()
                .HasIndex(n => new { n.UserId, n.IsRead });

            modelBuilder.Entity<Notification>()
                .HasIndex(n => new { n.UserId, n.CreatedAt });

            modelBuilder.Entity<Channel>()
                .HasIndex(c => c.ServerId);

            // AltSuspicion
            modelBuilder.Entity<AltSuspicion>()
                .HasIndex(s => new { s.UserId1, s.UserId2 })
                .IsUnique();

            modelBuilder.Entity<AltSuspicion>()
                .HasOne(s => s.User1)
                .WithMany()
                .HasForeignKey(s => s.UserId1)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<AltSuspicion>()
                .HasOne(s => s.User2)
                .WithMany()
                .HasForeignKey(s => s.UserId2)
                .OnDelete(DeleteBehavior.Restrict);

            // ── Torvex Gold ────────────────────────────────────────────────
            modelBuilder.Entity<TorvexGoldSubscription>()
                .HasOne(g => g.User)
                .WithMany(u => u.GoldSubscriptions)
                .HasForeignKey(g => g.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TorvexGoldSubscription>()
                .HasIndex(g => g.UserId);

            modelBuilder.Entity<TorvexGoldSubscription>()
                .HasIndex(g => g.StripeSubscriptionId);

            // ── Message Attachments ────────────────────────────────────────
            modelBuilder.Entity<MessageAttachment>()
                .HasOne(a => a.Message)
                .WithMany(m => m.Attachments)
                .HasForeignKey(a => a.MessageId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<MessageAttachment>()
                .HasOne(a => a.Uploader)
                .WithMany()
                .HasForeignKey(a => a.UploaderId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<MessageAttachment>()
                .HasIndex(a => a.MessageId);

            modelBuilder.Entity<MessageAttachment>()
                .HasIndex(a => a.UploaderId);

            // ── Peepo Rarity Votes ─────────────────────────────────────────
            modelBuilder.Entity<PeepoRarityVote>()
                .HasKey(v => new { v.PeepoName, v.IpAddress });
        }

        // Converts PascalCase to snake_case
        private static string ToSnakeCase(string name) =>
            string.Concat(name.Select((c, i) =>
                i > 0 && char.IsUpper(c)
                    ? "_" + char.ToLower(c)
                    : char.ToLower(c).ToString()));
    }

}
