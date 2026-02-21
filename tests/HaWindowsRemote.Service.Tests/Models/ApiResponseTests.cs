using HaWindowsRemote.Service.Models;
using Shouldly;

namespace HaWindowsRemote.Service.Tests.Models;

public class ApiResponseTests
{
    [Fact]
    public void Ok_SetsSuccessTrue()
    {
        var response = ApiResponse.Ok();

        response.Success.ShouldBeTrue();
        response.Message.ShouldBeNull();
    }

    [Fact]
    public void Ok_WithMessage_SetsMessage()
    {
        var response = ApiResponse.Ok("done");

        response.Success.ShouldBeTrue();
        response.Message.ShouldBe("done");
    }

    [Fact]
    public void Fail_SetsSuccessFalse()
    {
        var response = ApiResponse.Fail("error");

        response.Success.ShouldBeFalse();
        response.Message.ShouldBe("error");
    }

    [Fact]
    public void OkGeneric_SetsData()
    {
        var data = new HealthResponse { Status = "ok" };
        var response = ApiResponse.Ok(data);

        response.Success.ShouldBeTrue();
        response.Data.ShouldNotBeNull();
        response.Data.Status.ShouldBe("ok");
    }
}
