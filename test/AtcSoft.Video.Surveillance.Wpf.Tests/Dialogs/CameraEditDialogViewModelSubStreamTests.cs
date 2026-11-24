namespace AtcSoft.Video.Surveillance.Wpf.Dialogs;

/// <summary>
/// The camera dialog edits the sub-stream: a path, and a port where 0 is the main stream's.
/// </summary>
public sealed class CameraEditDialogViewModelSubStreamTests
{
    [Fact]
    public void The_Dialog_Shows_The_Camera_Sub_Stream()
    {
        var vm = new CameraEditDialogViewModel(TestCameras.Network() with { SubStreamPath = "/stream2", SubStreamPort = 8554 });

        (vm.SubStreamPath, vm.SubStreamPort).Should().Be(("/stream2", 8554));
    }

    [Fact]
    public void An_Update_Sends_The_Sub_Stream()
    {
        var vm = new CameraEditDialogViewModel(TestCameras.Network())
        {
            SubStreamPath = " /stream2 ",
            SubStreamPort = 8554,
        };

        var request = vm.BuildUpdateRequest();

        (request.SubStreamPath, request.SubStreamPort).Should().Be(("/stream2", 8554));
    }

    [Fact]
    public void An_Update_Without_A_Sub_Stream_Removes_It_On_The_Server()
    {
        var vm = new CameraEditDialogViewModel(TestCameras.Network() with { SubStreamPath = "/stream2", SubStreamPort = 8554 })
        {
            SubStreamPath = string.Empty,
        };

        var request = vm.BuildUpdateRequest();

        (request.SubStreamPath, request.SubStreamPort).Should().Be((string.Empty, 0));
    }

    [Fact]
    public void A_New_Camera_Without_A_Sub_Stream_Leaves_It_Out()
    {
        var request = new CameraEditDialogViewModel().BuildCreateRequest();

        (request.SubStreamPath, request.SubStreamPort).Should().Be((null, null));
    }

    [Fact]
    public void A_New_Camera_With_A_Sub_Stream_On_The_Main_Port_Sends_No_Port()
    {
        var vm = new CameraEditDialogViewModel { SubStreamPath = "/stream2", SubStreamPort = 0 };

        var request = vm.BuildCreateRequest();

        (request.SubStreamPath, request.SubStreamPort).Should().Be(("/stream2", null));
    }
}
