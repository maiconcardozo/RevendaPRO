using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using RevendaPro.Api.Authorization;
using RevendaPro.Api.Controllers;
using RevendaPro.Infrastructure.Screens;

namespace RevendaPro.Tests.Unit
{
    /// <summary>
    /// The permission matrix, checked statically.
    ///
    /// The risk this guards is precise: someone adds an endpoint and forgets the guard.
    /// Hitting a live API would catch it too, but only for the routes the test remembers to
    /// call — and a brand new unguarded route is exactly the one nobody remembers. Walking
    /// the assembly catches every action, including the ones added tomorrow.
    /// </summary>
    public class ApiGuardTests
    {
        private static readonly Assembly Api = typeof(AuthController).Assembly;

        private static IEnumerable<Type> Controllers =>
            Api.GetTypes().Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract);

        private static IEnumerable<(Type Controller, MethodInfo Action)> Actions =>
            Controllers.SelectMany(
                c => c.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                      .Where(m => !m.IsSpecialName && m.GetCustomAttributes<HttpMethodAttribute>().Any()),
                (c, m) => (c, m));

        [Fact]
        public void EveryActionIsEitherAuthorizedOrExplicitlyAnonymous()
        {
            var unguarded = Actions
                .Where(a =>
                    a.Action.GetCustomAttribute<AllowAnonymousAttribute>() is null
                    && a.Action.GetCustomAttribute<AuthorizeAttribute>() is null
                    && a.Controller.GetCustomAttribute<AuthorizeAttribute>() is null)
                .Select(a => $"{a.Controller.Name}.{a.Action.Name}")
                .ToList();

            unguarded.Should().BeEmpty(
                "an action reaches the database, so it declares Authorize or AllowAnonymous");
        }

        /// <summary>
        /// Every authenticated action either requires a screen or is on the short list of
        /// endpoints that only need a valid session. The list is deliberately explicit: a new
        /// controller without a screen guard fails until someone states the intent here.
        /// </summary>
        [Fact]
        public void EveryAuthenticatedActionRequiresAScreenOrIsListedAsSessionOnly()
        {
            string[] sessionOnly =
            [
                // The session itself: requiring a screen here would lock a user out of the
                // very call that tells them which screens they hold.
                $"{nameof(AuthController)}.Me",
                $"{nameof(AuthController)}.Logout",

                // Anyone signed in has to see their own avatar in the sidebar, even without
                // access to user administration.
                $"{nameof(UserPhotosController)}.Get"
            ];

            var missing = Actions
                .Where(a => a.Action.GetCustomAttribute<AllowAnonymousAttribute>() is null)
                .Where(a =>
                    a.Controller.GetCustomAttribute<RequireScreenAttribute>() is null
                    && a.Action.GetCustomAttribute<RequireScreenAttribute>() is null)
                .Select(a => $"{a.Controller.Name}.{a.Action.Name}")
                .Except(sessionOnly)
                .ToList();

            missing.Should().BeEmpty(
                "an authenticated endpoint declares which screen it needs, or joins the "
                + "session-only list with a reason");
        }

        /// <summary>
        /// A guard pointing at a screen key that the catalog never declares would let
        /// everyone through, because no role can ever hold it.
        /// </summary>
        [Fact]
        public void EveryRequiredScreenExistsInTheCatalog()
        {
            var declared = ScreenCatalog.Screens
                .Select(s => s.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var required = Controllers
                .SelectMany(c => c.GetCustomAttributes<RequireScreenAttribute>()
                    .Concat(c.GetMethods().SelectMany(m => m.GetCustomAttributes<RequireScreenAttribute>())))
                .Select(a => (string)a.Arguments![0]!)
                .Distinct()
                .ToList();

            required.Should().NotBeEmpty();
            required.Should().OnlyContain(key => declared.Contains(key));
        }

        [Fact]
        public void EveryActionDeclaresItsResponses()
        {
            var undocumented = Actions
                .Where(a => !a.Action.GetCustomAttributes<ProducesResponseTypeAttribute>().Any())
                .Select(a => $"{a.Controller.Name}.{a.Action.Name}")
                .ToList();

            undocumented.Should().BeEmpty("Swagger is the contract the frontend reads");
        }

        [Fact]
        public void ControllersStayThin()
        {
            // A controller that grew its own dependencies is doing work that belongs in a
            // handler. Mediator plus, at most, the current user is the whole budget.
            var fat = Controllers
                .Select(c => (Name: c.Name, Parameters: c.GetConstructors().First().GetParameters()))
                .Where(c => c.Parameters.Length > 2)
                .Select(c => c.Name)
                .ToList();

            fat.Should().BeEmpty("controllers delegate to MediatR instead of orchestrating");
        }
    }
}
