@echo off
setlocal enabledelayedexpansion

REM ============================================================
REM Messaging – Bootstrap Script (.NET 10)
REM ============================================================

REM ---- Create solution ----
dotnet new sln -n Messaging

REM ---- Base folders ----
mkdir src
mkdir test
mkdir ui

REM ============================================================
REM PLATFORM PROJECTS (Channel-agnostic)
REM ============================================================

dotnet new classlib -n Messaging.Platform.Core -f net10.0 -o src/Platform/Core
dotnet new web     -n Messaging.Platform.Api  -f net10.0 -o src/Platform/Api

REM ============================================================
REM EMAIL CHANNEL PROJECTS
REM ============================================================

dotnet new classlib -n Messaging.Email.Core      -f net10.0 -o src/Email/Core
dotnet new classlib -n Messaging.Email.Templates -f net10.0 -o src/Email/Templates
dotnet new classlib -n Messaging.Email.Queue     -f net10.0 -o src/Email/Queue
dotnet new classlib -n Messaging.Email.Review    -f net10.0 -o src/Email/Review
dotnet new classlib -n Messaging.Email.Delivery  -f net10.0 -o src/Email/Delivery

REM ============================================================
REM WORKERS
REM ============================================================

mkdir src\Workers
dotnet new worker -n Messaging.Workers.EmailDelivery -f net10.0 -o src/Workers/EmailDelivery

REM ============================================================
REM TEST PROJECTS
REM ============================================================

dotnet new xunit -n Messaging.Platform.Core.Tests -f net10.0 -o test/Platform.Core.Tests
dotnet new xunit -n Messaging.Email.Queue.Tests   -f net10.0 -o test/Email.Queue.Tests
dotnet new xunit -n Messaging.Email.Review.Tests  -f net10.0 -o test/Email.Review.Tests

REM ============================================================
REM ADD PROJECTS TO SOLUTION
REM ============================================================

dotnet sln add src\Platform\Core\Messaging.Platform.Core.csproj
dotnet sln add src\Platform\Api\Messaging.Platform.Api.csproj

dotnet sln add src\Email\Core\Messaging.Email.Core.csproj
dotnet sln add src\Email\Templates\Messaging.Email.Templates.csproj
dotnet sln add src\Email\Queue\Messaging.Email.Queue.csproj
dotnet sln add src\Email\Review\Messaging.Email.Review.csproj
dotnet sln add src\Email\Delivery\Messaging.Email.Delivery.csproj

dotnet sln add src\Workers\EmailDelivery\Messaging.Workers.EmailDelivery.csproj

dotnet sln add test\Platform.Core.Tests\Messaging.Platform.Core.Tests.csproj
dotnet sln add test\Email.Queue.Tests\Messaging.Email.Queue.Tests.csproj
dotnet sln add test\Email.Review.Tests\Messaging.Email.Review.Tests.csproj

REM ============================================================
REM PROJECT REFERENCES (STRICT DIRECTIONALITY)
REM ============================================================

REM ---- Platform ----
dotnet add src\Platform\Api reference src\Platform\Core

REM ---- Email Channel ----
dotnet add src\Email\Core      reference src\Platform\Core
dotnet add src\Email\Templates reference src\Email\Core
dotnet add src\Email\Queue     reference src\Email\Core
dotnet add src\Email\Queue     reference src\Platform\Core
dotnet add src\Email\Review    reference src\Email\Core
dotnet add src\Email\Review    reference src\Platform\Core
dotnet add src\Email\Delivery  reference src\Email\Core

REM ---- Worker ----
dotnet add src\Workers\EmailDelivery reference src\Email\Queue
dotnet add src\Workers\EmailDelivery reference src\Email\Delivery
dotnet add src\Workers\EmailDelivery reference src\Platform\Core

REM ---- Tests ----
dotnet add test\Platform.Core.Tests reference src\Platform\Core
dotnet add test\Email.Queue.Tests   reference src\Email\Queue
dotnet add test\Email.Review.Tests  reference src\Email\Review

REM ============================================================
REM TEST PACKAGES
REM ============================================================

dotnet add test\Platform.Core.Tests package FluentAssertions
dotnet add test\Email.Queue.Tests   package FluentAssertions
dotnet add test\Email.Review.Tests  package FluentAssertions

dotnet add test\Email.Queue.Tests   package NSubstitute
dotnet add test\Email.Review.Tests  package NSubstitute

REM ============================================================
REM FINAL CHECK
REM ============================================================

dotnet restore
dotnet build
dotnet test

echo.
echo ============================================================
echo Messaging bootstrap complete
echo UI lives in /ui (React/Vite – not a .NET project)
echo Worker: Messaging.Workers.EmailDelivery
echo ============================================================
