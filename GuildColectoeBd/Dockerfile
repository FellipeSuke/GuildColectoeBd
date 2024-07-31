# Use the official .NET 8 SDK as the base image
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

# Set the working directory
WORKDIR /app

# Copy the project files and restore any dependencies
COPY *.csproj ./
RUN dotnet restore

# Copy the rest of the application code
COPY . ./

# Build the application
RUN dotnet build -c Release -o /app/build

# Use the official .NET 8 runtime as the base image
FROM mcr.microsoft.com/dotnet/aspnet:8.0

# Set the working directory
WORKDIR /app

# Copy the build output from the build stage
COPY --from=build /app/build .

# Install MySQL client and dependencies
RUN apt-get update && \
    apt-get install -y \
    default-mysql-client \
    && apt-get clean

# Print MySQL version
RUN mysql --version

# Define the entry point for the container
COPY entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh
ENTRYPOINT ["/entrypoint.sh"]
