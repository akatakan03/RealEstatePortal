using System.Reflection;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using RealEstatePortal.Application.Appointments;
using RealEstatePortal.Application.Appointments.EventHandlers;
using RealEstatePortal.Application.Common.Behaviours;

namespace RealEstatePortal.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddValidatorsFromAssembly(assembly);

        // Shared composer for the appointment event handlers.
        services.AddScoped<AppointmentNotifier>();

        // Shared open-slot computation used by the slot picker, the booking command, and the
        // counter-proposal command.
        services.AddScoped<IAgentScheduleService, AgentScheduleService>();

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddOpenRequestPreProcessor(typeof(LoggingBehaviour<>));
            cfg.AddOpenBehavior(typeof(ValidationBehaviour<,>));
            cfg.AddOpenBehavior(typeof(PerformanceBehaviour<,>));
        });

        return services;
    }
}