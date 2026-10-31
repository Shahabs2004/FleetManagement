namespace FleetManagement.Domain.Common;

/// <summary>
/// کلاس پایه موجودیت‌های دارای شناسه.
/// </summary>
public abstract class Entity
{
    /// <summary>
    /// شناسه یکتای موجودیت.
    /// </summary>
    public Guid Id { get; protected set; } = Guid.NewGuid();
}
