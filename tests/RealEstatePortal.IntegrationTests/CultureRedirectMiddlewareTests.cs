using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using RealEstatePortal.Web.Localization;
using Shouldly;
using Xunit;

namespace RealEstatePortal.IntegrationTests;

/// Guards the highest-severity localization regression: an error page re-executed by
/// UseExceptionHandler must keep the failed request's status. A culture-less path used to be
/// turned into a 302, which replaced the 500 — so uptime checks and crawlers read a crash as a
/// success. These tests need no host, so they stay outside the fixture collections.
public class CultureRedirectMiddlewareTests
{
    private static DefaultHttpContext GetRequest(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = path;
        return context;
    }

    [Fact]
    public async Task ErrorReexecution_GetsALanguageByRewritingInPlace_NotByRedirecting()
    {
        var context = GetRequest("/Home/Error");
        context.Response.StatusCode = 500;   // what UseExceptionHandler set before re-executing
        context.Features.Set<IExceptionHandlerPathFeature>(new ExceptionHandlerFeature
        {
            Path = "/en/Dashboard",
            Error = new InvalidOperationException("boom")
        });

        string? seenByNext = null;
        var middleware = new CultureRedirectMiddleware(c =>
        {
            seenByNext = c.Request.Path.Value;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        // The pipeline continues (so the error view renders here) at a path routing can match,
        // and the 500 is untouched — no redirect, no Location header.
        seenByNext.ShouldBe("/tr/Home/Error");
        context.Response.StatusCode.ShouldBe(500);
        context.Response.Headers.ContainsKey("Location").ShouldBeFalse();
    }

    [Fact]
    public async Task OrdinaryCultureLessGet_StillRedirectsToTheDefaultLanguage()
    {
        var context = GetRequest("/listings");

        var nextCalled = false;
        var middleware = new CultureRedirectMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        nextCalled.ShouldBeFalse();
        context.Response.StatusCode.ShouldBe(302);
        context.Response.Headers.Location.ToString().ShouldBe("/tr/listings");
    }
}
