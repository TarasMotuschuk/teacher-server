namespace Teacher.Common.Contracts.Testing;

public enum QuestionType
{
    SingleChoice = 0,
    MultipleChoice = 1,
    Ordering = 2,
    Matching = 3,
    TrueFalseGroup = 4,
    NumericInputGroup = 5,
    TextInput = 6,
    ImagePoint = 7,
    LetterOrdering = 8,
}

public enum TestStatus
{
    Draft = 0,
    Published = 1,
    Archived = 2,
}

public enum AssignmentStatus
{
    Draft = 0,
    Published = 1,
    Closed = 2,
    Archived = 3,
}

public enum AttemptStatus
{
    NotStarted = 0,
    InProgress = 1,
    Submitted = 2,
    Scored = 3,
    Expired = 4,
    Cancelled = 5,
}

public enum AssetKind
{
    Image = 0,
}

public enum AudienceType
{
    Class = 0,
    StudentSelection = 1,
}
