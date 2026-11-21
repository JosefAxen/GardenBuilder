using GardenBuilder.Contracts.Gardens;
using GardenBuilder.Contracts.Scenes;
using GardenBuilder.Modules.Ai.Features.DreamImages;
using GardenBuilder.SharedKernel;

namespace GardenBuilder.UnitTests.Ai;

public sealed class DreamPhotosTests
{
    /// <summary>Stands south of the garden at (10, -5) and looks due north (heading 0).</summary>
    private static readonly SceneView NorthView =
        new(Guid.NewGuid(), "Från gatan", new Point3(10, -5, 1.7), new Point3(10, 10, 1), 50);

    private static GardenPhotoInfo Photo(CameraPose? camera) => new(Guid.NewGuid(), Guid.NewGuid(), 4000, 3000, camera);

    private static CameraPose Pose(double x, double y, double heading) => new(new Point2(x, y), heading);

    [Fact]
    public void The_two_photos_facing_the_view_are_chosen_closest_heading_first()
    {
        var slightlyOff = Photo(Pose(10, 0, 30));
        var away = Photo(Pose(10, 0, 180));
        var straight = Photo(Pose(12, 0, 350));

        var chosen = DreamPhotos.Choose(NorthView, [slightlyOff, away, straight], sendPhotos: true);

        Assert.Equal([straight.Id, slightlyOff.Id], chosen);
    }

    [Fact]
    public void A_photo_more_than_120_degrees_off_is_left_out()
    {
        var off = Photo(Pose(10, 0, 121));
        var edge = Photo(Pose(10, 0, 240));

        Assert.Equal([edge.Id], DreamPhotos.Choose(NorthView, [off, edge], sendPhotos: true));
    }

    [Fact]
    public void Equal_headings_are_decided_by_distance_to_the_eye()
    {
        var far = Photo(Pose(10, 15, 0));
        var near = Photo(Pose(10, -3, 0));

        Assert.Equal([near.Id, far.Id], DreamPhotos.Choose(NorthView, [far, near], sendPhotos: true));
    }

    [Fact]
    public void Without_poses_the_first_two_uploaded_are_chosen()
    {
        var first = Photo(null);
        var second = Photo(null);
        var third = Photo(null);

        Assert.Equal([first.Id, second.Id], DreamPhotos.Choose(NorthView, [first, second, third], sendPhotos: true));
    }

    [Fact]
    public void Photos_without_a_pose_are_not_used_when_some_have_one()
    {
        var unposed = Photo(null);
        var posed = Photo(Pose(10, 0, 0));

        Assert.Equal([posed.Id], DreamPhotos.Choose(NorthView, [unposed, posed], sendPhotos: true));
    }

    [Fact]
    public void Nothing_is_sent_when_the_customer_unticks_the_box()
    {
        Assert.Empty(DreamPhotos.Choose(NorthView, [Photo(Pose(10, 0, 0)), Photo(null)], sendPhotos: false));
    }

    [Fact]
    public void No_photos_gives_none()
    {
        Assert.Empty(DreamPhotos.Choose(NorthView, [], sendPhotos: true));
    }
}
