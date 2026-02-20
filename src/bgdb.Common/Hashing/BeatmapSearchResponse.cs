using System.Text.Json.Serialization;

namespace bgdb.Common.Hashing;

public record BeatmapSearchResponse(
    [property: JsonPropertyName("beatmapsets")] IReadOnlyList<Beatmapset> Beatmapsets,
    [property: JsonPropertyName("search")] Search Search,
    [property: JsonPropertyName("recommended_difficulty")] object RecommendedDifficulty,
    [property: JsonPropertyName("error")] object Error,
    [property: JsonPropertyName("total")] int Total,
    [property: JsonPropertyName("cursor")] Cursor Cursor,
    [property: JsonPropertyName("cursor_string")] string CursorString
);

public record Availability(
    [property: JsonPropertyName("download_disabled")] bool DownloadDisabled,
    [property: JsonPropertyName("more_information")] object MoreInformation
);

public record Beatmap(
    [property: JsonPropertyName("beatmapset_id")] int BeatmapsetId,
    [property: JsonPropertyName("difficulty_rating")] double DifficultyRating,
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("mode")] string Mode,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("total_length")] int TotalLength,
    [property: JsonPropertyName("user_id")] int UserId,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("accuracy")] double Accuracy,
    [property: JsonPropertyName("ar")] double Ar,
    [property: JsonPropertyName("bpm")] double Bpm,
    [property: JsonPropertyName("convert")] bool Convert,
    [property: JsonPropertyName("count_circles")] int CountCircles,
    [property: JsonPropertyName("count_sliders")] int CountSliders,
    [property: JsonPropertyName("count_spinners")] int CountSpinners,
    [property: JsonPropertyName("cs")] double Cs,
    [property: JsonPropertyName("deleted_at")] object DeletedAt,
    [property: JsonPropertyName("drain")] double Drain,
    [property: JsonPropertyName("hit_length")] int HitLength,
    [property: JsonPropertyName("is_scoreable")] bool IsScoreable,
    [property: JsonPropertyName("last_updated")] DateTime LastUpdated,
    [property: JsonPropertyName("mode_int")] int ModeInt,
    [property: JsonPropertyName("passcount")] int Passcount,
    [property: JsonPropertyName("playcount")] int Playcount,
    [property: JsonPropertyName("ranked")] int Ranked,
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("checksum")] string Checksum,
    [property: JsonPropertyName("max_combo")] int MaxCombo
);

public record Beatmapset(
    [property: JsonPropertyName("artist")] string Artist,
    [property: JsonPropertyName("artist_unicode")] string ArtistUnicode,
    [property: JsonPropertyName("covers")] Covers Covers,
    [property: JsonPropertyName("creator")] string Creator,
    [property: JsonPropertyName("favourite_count")] int FavouriteCount,
    [property: JsonPropertyName("genre_id")] int GenreId,
    [property: JsonPropertyName("hype")] object Hype,
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("language_id")] int LanguageId,
    [property: JsonPropertyName("nsfw")] bool Nsfw,
    [property: JsonPropertyName("offset")] int Offset,
    [property: JsonPropertyName("play_count")] int PlayCount,
    [property: JsonPropertyName("preview_url")] string PreviewUrl,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("spotlight")] bool Spotlight,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("title_unicode")] string TitleUnicode,
    [property: JsonPropertyName("track_id")] int? TrackId,
    [property: JsonPropertyName("user_id")] int UserId,
    [property: JsonPropertyName("video")] bool Video,
    [property: JsonPropertyName("bpm")] double Bpm,
    [property: JsonPropertyName("can_be_hyped")] bool CanBeHyped,
    [property: JsonPropertyName("deleted_at")] object DeletedAt,
    [property: JsonPropertyName("discussion_enabled")] bool DiscussionEnabled,
    [property: JsonPropertyName("discussion_locked")] bool DiscussionLocked,
    [property: JsonPropertyName("is_scoreable")] bool IsScoreable,
    [property: JsonPropertyName("last_updated")] DateTime LastUpdated,
    [property: JsonPropertyName("legacy_thread_url")] string LegacyThreadUrl,
    [property: JsonPropertyName("nominations_summary")] NominationsSummary NominationsSummary,
    [property: JsonPropertyName("ranked")] int Ranked,
    [property: JsonPropertyName("ranked_date")] DateTime RankedDate,
    [property: JsonPropertyName("rating")] double Rating,
    [property: JsonPropertyName("storyboard")] bool Storyboard,
    [property: JsonPropertyName("submitted_date")] DateTime SubmittedDate,
    [property: JsonPropertyName("tags")] string Tags,
    [property: JsonPropertyName("availability")] Availability Availability,
    [property: JsonPropertyName("beatmaps")] IReadOnlyList<Beatmap> Beatmaps,
    [property: JsonPropertyName("pack_tags")] IReadOnlyList<string> PackTags
);

public record Covers(
    [property: JsonPropertyName("cover")] string Cover,
    [property: JsonPropertyName("cover@2x")] string Cover2x,
    [property: JsonPropertyName("card")] string Card,
    [property: JsonPropertyName("card@2x")] string Card2x,
    [property: JsonPropertyName("list")] string List,
    [property: JsonPropertyName("list@2x")] string List2x,
    [property: JsonPropertyName("slimcover")] string Slimcover,
    [property: JsonPropertyName("slimcover@2x")] string Slimcover2x
);

public record Cursor(
    [property: JsonPropertyName("approved_date")] long ApprovedDate,
    [property: JsonPropertyName("id")] int Id
);

public record NominationsSummary(
    [property: JsonPropertyName("current")] int Current,
    [property: JsonPropertyName("eligible_main_rulesets")] IReadOnlyList<string> EligibleMainRulesets,
    [property: JsonPropertyName("required_meta")] RequiredMeta RequiredMeta
);

public record RequiredMeta(
    [property: JsonPropertyName("main_ruleset")] int MainRuleset,
    [property: JsonPropertyName("non_main_ruleset")] int NonMainRuleset
);

public record Search(
    [property: JsonPropertyName("sort")] string Sort
);
