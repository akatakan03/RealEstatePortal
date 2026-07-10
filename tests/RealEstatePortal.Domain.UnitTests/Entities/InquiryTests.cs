using NetCloudFramework.Shouldly;
using RealEstatePortal.Domain.Entities;
using RealEstatePortal.Domain.Enums;
using Xunit;

namespace RealEstatePortal.Domain.UnitTests.Entities;

public class InquiryTests
{
    [Fact]
    public void NewInquiry_DefaultsToNew()
    {
        new Inquiry().Status.ShouldBe(InquiryStatus.New);
    }

    [Fact]
    public void MarkAsRead_SetsStatusToRead()
    {
        var inquiry = new Inquiry();

        inquiry.MarkAsRead();

        inquiry.Status.ShouldBe(InquiryStatus.Read);
    }

    [Fact]
    public void MarkAsHandled_SetsStatusToHandled()
    {
        var inquiry = new Inquiry();

        inquiry.MarkAsHandled();

        inquiry.Status.ShouldBe(InquiryStatus.Handled);
    }
}