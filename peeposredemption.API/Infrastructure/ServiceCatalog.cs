namespace peeposredemption.API.Infrastructure;

public record ServicePackage(
    string Name,
    string Icon,
    string Price,
    string? PriceNote,
    string Blurb,
    string[] Features,
    bool IsSubscription,
    bool IsBotAddon = false,
    string? Slug = null,
    long? PriceCents = null);

/// <summary>
/// The sellable service catalog. Rendered on /Packages and used for the
/// package dropdown on /Contact — edit packages and prices here only.
/// Packages with a <see cref="ServicePackage.PriceCents"/> are checkout-able
/// (Stripe); the rest go through the /Contact quote flow.
/// </summary>
public static class ServiceCatalog
{
    // ── Discord AI add-on: prepaid credit pack ──────────────────────────
    // Face value equals the price: AiPackCreditUsd is the AI usage the bot
    // grants for AiPackPriceCents, and the two are always the same number.
    // The bot mirrors the price in its own .env (AI_CREDIT_PACK_USD) — change
    // both together.
    public const string AiPackSlug = "discord-ai-addon";
    public const long AiPackPriceCents = 2000;
    public const decimal AiPackCreditUsd = 20m;

    // ── Discord Logging Pro: the bot's long message archive ───────────
    // Free tier (every server, no purchase): deleted/edited messages AND
    // their images recoverable for 24h. Pro = 90 days of text, 30 days of
    // files, searchable. Priced per server; the retention windows live in the
    // bot's env (MSGLOG_PRO_*).
    // Checkout is not built yet: the card routes to /Contact and the operator
    // grants with `/msglog pro-grant` until the Stripe subscription ships.
    public const string LoggingProSlug = "discord-logging-pro";
    public const string LoggingProMonthly = "$4/mo";
    public const string LoggingProYearly = "$36/yr";

    // ── AltGuard: verification gate + alt detection ───────────────────
    // The gate itself is free (security shouldn't be paywalled — the
    // calibration IS the product's credibility). Sold on top: reviewed
    // verdicts (Security AI, not built yet — early access via /Contact)
    // and human incident response, which IS available today.
    // Design: altguard/docs/MULTI-SERVER-DESIGN.md.
    public const string AltGuardSlug = "discord-altguard";
    public const string SecurityAiSlug = "discord-security-ai";
    public const string IncidentResponseSlug = "discord-incident-response";

    public static readonly ServicePackage[] Packages =
    {
        // ── Monthly subscriptions ────────────────────────────────────
        new(
            "Managed Sysadmin — Essentials",
            "🖥️",
            "$199/mo",
            "cancel anytime",
            "Your systems, looked after — so small problems never become big ones.",
            new[]
            {
                "Monitoring & alerting on your servers and services",
                "Patches and updates applied on a schedule",
                "Backup checks — verified, not assumed",
                "Email support with next-business-day response",
                "Monthly health report in plain English"
            },
            IsSubscription: true),
        new(
            "Managed Sysadmin — Business",
            "🏢",
            "$449/mo",
            "cancel anytime",
            "Everything in Essentials, plus the response times a business actually needs.",
            new[]
            {
                "Everything in Essentials",
                "Priority same-day response",
                "User & access management (onboarding/offboarding)",
                "Security hardening and account audits",
                "Quarterly planning review"
            },
            IsSubscription: true),
        new(
            "Managed Cloud Hosting — your cloud account",
            "☁️",
            "from $99/mo",
            "+ one-time setup",
            "Hosting on YOUR AWS, Azure, or GCP account. You own the infrastructure and the bill — it just runs like someone's paid to care, because someone is.",
            new[]
            {
                "Setup, deployment, SSL, and DNS on your cloud account",
                "Monitoring, updates, and incident response",
                "Backups configured and tested",
                "Monthly cost review — no surprise cloud bills",
                "No lock-in: it's your account, always"
            },
            IsSubscription: true),
        new(
            "Discord AI Add-on — Torvex Forerunner",
            "🤖",
            "$20",
            "prepaid credit pack · top up anytime",
            "AI chat for your Discord server, pay-per-use: $20 buys $20 of AI usage for your server — it never expires and only shrinks when the AI is actually used. Members talk to the Torvex Forerunner bot with /ask or by pinging it; the bot's core features stay free.",
            new[]
            {
                "/ask and ping-to-chat answers in your server",
                "Daily free energy for every member — no per-user fees",
                "Credit is applied to your server automatically after checkout",
                "AI pauses when credit runs out — never a surprise bill",
                "Privacy-scoped: it reads only the channel it's asked in — context never crosses channels",
                "Requires the free Torvex Forerunner bot"
            },
            IsSubscription: false,
            IsBotAddon: true,
            Slug: AiPackSlug,
            PriceCents: AiPackPriceCents),
        new(
            "Discord Logging Pro — Torvex Forerunner",
            "📜",
            LoggingProMonthly,
            $"or {LoggingProYearly} (save 25%) · per server",
            "Every server with the bot already gets the free log: deleted and edited messages — images included — recoverable for 24 hours, with who-deleted-it and voice mute/deafen tracking. Pro keeps a real archive you can search when the question comes up a week later.",
            new[]
            {
                "90 days of deleted-message history (Quark Pro: 4 weeks)",
                "30 days of deleted images and files, up to 1 GB per server",
                "Searchable: a member's deleted messages, name and timeout history",
                "Bulk-delete transcripts attributed to the moderator",
                "No per-channel message caps, ever",
                "Free tier — 24h window, server mute/deafen logs, bot-action logs — stays free"
            },
            IsSubscription: true,
            IsBotAddon: true,
            Slug: LoggingProSlug),
        new(
            "AltGuard — verification & alt detection",
            "🧿",
            "Free",
            "included with the bot",
            "A verification gate that catches alt accounts and ban evaders by device and connection, not by IP guesswork — with two months of false-positive calibration behind every verdict. Members who are wrongly flagged get a real appeal, and your mods get one click to release them.",
            new[]
            {
                "Device + connection matching, tuned against real false positives",
                "Ban-evasion detection scoped to YOUR server's ban list",
                "Roles saved on hold and restored exactly on release",
                "Runs alongside carl-bot or your existing verification — observe, assist, or full gate",
                "What happens in other servers never punishes anyone in yours",
                "Self-service release: your mods decide, not us"
            },
            IsSubscription: false,
            IsBotAddon: true,
            Slug: AltGuardSlug),
        new(
            "Security AI — reviewed verdicts",
            "🔎",
            "from $5/mo",
            "three tiers · per server",
            "Every flagged member gets a written second opinion before your mods decide: what the evidence actually shows, what argues against it, and a recommendation. The reviewer reasons over the case — never over anyone's identity or raw data, which it is never given.",
            new[]
            {
                "Standard — $5/mo: a written assessment on every flagged case, not just a score",
                "Advanced — $12/mo: argues both sides and rules between them, with your server's past outcomes in context",
                "Elite — from $79/mo: adversarial review panel, a security event feed of your server for your own SIEM, and one incident response included each quarter",
                "Every tier reasons from evidence it can't leak — no identities, no raw telemetry",
                "Case files with reliability notes and past outcomes, free either way",
                "Early access — first servers help set the shape"
            },
            IsSubscription: true,
            IsBotAddon: true,
            Slug: SecurityAiSlug),
        new(
            "Incident Response — Discord",
            "🚨",
            "$49",
            "per incident · same-day",
            "Something happened: a raid, a nuke attempt, a ban evader you can't pin down, a link that stole someone's account. We investigate with the full toolset and tell you what actually happened, who did it, and what to change so it doesn't happen twice.",
            new[]
            {
                "Written findings: what happened, which accounts are linked, how confident",
                "Actor tripwires configured so their return is caught automatically",
                "Server hardening review off the back of the incident",
                "Findings cover your server only — nobody else's members are disclosed",
                "Included quarterly with Security AI Elite"
            },
            IsSubscription: false,
            IsBotAddon: true,
            Slug: IncidentResponseSlug),

        // ── One-off projects ─────────────────────────────────────────
        new(
            "Network Install & Wi-Fi",
            "🌐",
            "from $499",
            "hardware billed at cost",
            "An office network done properly: coverage where you need it, security by default, documentation you can hand to the next person.",
            new[]
            {
                "Site survey and equipment recommendation",
                "Router, switch, AP, and VPN setup",
                "Guest and staff network separation",
                "Remote access configured securely",
                "Full documentation of what was built"
            },
            IsSubscription: false),
        new(
            "ERP Customization & Integration",
            "⚙️",
            "from $1,500",
            "scoped per project",
            "Your ERP, made to fit how you actually work — custom reports, integrations, and automations on the system you already own.",
            new[]
            {
                "Custom reports and dashboards",
                "Integrations with the tools around your ERP",
                "Workflow automation for repetitive entry",
                "Data cleanup and migrations",
                "Training for your team on what changed"
            },
            IsSubscription: false),
        new(
            "Custom Software / Website Build",
            "🧩",
            "from $2,500",
            "scoped per project",
            "Internal tools, dashboards, and websites built around your business — delivered hosting-ready with handoff documentation.",
            new[]
            {
                "Scoped, fixed-price build — no hourly creep",
                "Web app, internal tool, or business website",
                "Hosting-ready (pairs with Managed Cloud Hosting)",
                "Source code and documentation are yours",
                "30 days of post-launch fixes included"
            },
            IsSubscription: false),
        new(
            "Discord Community Setup",
            "🛡️",
            "from $199",
            "one-time",
            "A community server built by someone who runs one: structure, moderation, verification, and bots that actually work.",
            new[]
            {
                "Channel and role structure designed for your community",
                "Moderation and anti-raid tooling configured",
                "Member verification and alt protection",
                "Custom bot setup and automation",
                "Staff onboarding notes included"
            },
            IsSubscription: false),
    };

    public static IEnumerable<string> Names => Packages.Select(p => p.Name);

    public static bool IsValidName(string? name) =>
        !string.IsNullOrWhiteSpace(name) && Packages.Any(p => p.Name == name);

    public static ServicePackage? BySlug(string? slug) =>
        string.IsNullOrWhiteSpace(slug) ? null : Packages.FirstOrDefault(p => p.Slug == slug);

    public static ServicePackage AiPack => Packages.First(p => p.Slug == AiPackSlug);
}
