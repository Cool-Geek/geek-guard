using Telegram.Bot.Exceptions;

namespace GeekGuard.Core.Messaging;

/// <summary>Why Telegram refused a moderation action, in terms an admin can act on.</summary>
public enum ActionProblem
{
    None = 0,

    /// <summary>The bot is not an admin, or lacks the right (e.g. "restrict members").</summary>
    MissingRights,

    /// <summary>The group is a basic group; muting needs a supergroup.</summary>
    SupergroupRequired,

    /// <summary>The target is an admin or the owner.</summary>
    TargetIsAdmin,

    /// <summary>The target is not (or no longer) in the group.</summary>
    NotAMember,

    /// <summary>Anything else; see <see cref="ActionResult.Detail"/>.</summary>
    Other,
}

/// <summary>The outcome of a moderation call: success, or the reason Telegram gave for refusing it.</summary>
public readonly record struct ActionResult(ActionProblem Problem, string? Detail = null)
{
    public static ActionResult Ok { get; } = new(ActionProblem.None);

    public bool Succeeded => Problem == ActionProblem.None;

    /// <summary>Maps Telegram's error text to a <see cref="ActionProblem"/>.</summary>
    public static ActionResult From(ApiRequestException ex)
    {
        var text = ex.Message.ToLowerInvariant();
        var problem =
            text.Contains("supergroup") ? ActionProblem.SupergroupRequired
            : text.Contains("not enough rights") || text.Contains("administrator rights") || text.Contains("chat_admin_required")
                || text.Contains("have no rights") ? ActionProblem.MissingRights
            : text.Contains("is an administrator") || text.Contains("chat owner") || text.Contains("restrict self")
                ? ActionProblem.TargetIsAdmin
            : text.Contains("participant_id_invalid") || text.Contains("user not found") || text.Contains("user_not_participant")
                ? ActionProblem.NotAMember
            : ActionProblem.Other;
        return new ActionResult(problem, ex.Message);
    }
}
