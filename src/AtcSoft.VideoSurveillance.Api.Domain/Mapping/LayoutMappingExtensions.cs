using CoreCameraLayout = AtcSoft.VideoSurveillance.Models.CameraLayout;
using CoreCameraLayoutItem = AtcSoft.VideoSurveillance.Models.CameraLayoutItem;

namespace AtcSoft.VideoSurveillance.Api.Domain.Mapping;

internal static class LayoutMappingExtensions
{
    public static Layout ToApiModel(this CoreCameraLayout core)
        => new(
            Id: core.Id,
            Name: core.Name,
            Rows: ComputeRows(core.Items.Count),
            Columns: ComputeColumns(core.Items.Count),
            Cameras: core.Items
                .Select(i => new LayoutItem(i.CameraId, i.OrderNumber))
                .ToList());

    public static CoreCameraLayout ToCoreModel(this CreateLayoutRequest request)
    {
        var totalSlots = request.Rows * request.Columns;
        var items = new List<CoreCameraLayoutItem>(totalSlots);
        for (var i = 0; i < totalSlots; i++)
        {
            items.Add(new CoreCameraLayoutItem { OrderNumber = i });
        }

        return new CoreCameraLayout
        {
            Name = request.Name,
            Items = items,
        };
    }

    public static void ApplyUpdate(
        this CoreCameraLayout core,
        UpdateLayoutRequest request)
    {
        if (!string.IsNullOrEmpty(request.Name))
        {
            core.Name = request.Name;
        }

        if (request.Cameras is { Count: > 0 })
        {
            core.Items = request.Cameras
                .Select(c => new CoreCameraLayoutItem
                {
                    CameraId = c.CameraId ?? Guid.Empty,
                    OrderNumber = c.Position ?? 0,
                })
                .ToList();
        }
        else if (request.Rows > 0 && request.Columns > 0)
        {
            var totalSlots = request.Rows.Value * request.Columns.Value;
            while (core.Items.Count < totalSlots)
            {
                core.Items.Add(new CoreCameraLayoutItem { OrderNumber = core.Items.Count });
            }

            if (core.Items.Count > totalSlots)
            {
                core.Items.RemoveRange(totalSlots, core.Items.Count - totalSlots);
            }
        }

        core.ModifiedAt = DateTime.UtcNow;
    }

    private static int ComputeRows(int itemCount)
        => itemCount <= 0 ? 1 : (int)Math.Ceiling(Math.Sqrt(itemCount));

    private static int ComputeColumns(int itemCount)
    {
        if (itemCount <= 0)
        {
            return 1;
        }

        var rows = (int)Math.Ceiling(Math.Sqrt(itemCount));
        return (int)Math.Ceiling((double)itemCount / rows);
    }
}