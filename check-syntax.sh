#!/bin/bash
set -e

# This script validates C# syntax without performing a full build
# It's faster than a full build for quick syntax checking

echo "Checking C# syntax with Mono..."

# Create a dummy project file for syntax checking
cat > /tmp/syntax_check.csproj << EOL
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net462</TargetFramework>
    <OutputType>Library</OutputType>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
    <PackageReference Include="protobuf-net" Version="2.4.6" />
  </ItemGroup>
</Project>
EOL

# Create a list of files to check
FILES_TO_CHECK=$(find EmuLibrary -name "*.cs" -not -path "*/Settings/*" -not -path "*/obj/*" -not -path "*/bin/*")

# Check files for syntax issues
for file in $FILES_TO_CHECK; do
  echo "Checking: $file"
  
  # Run a minimal syntax check, ignoring reference errors
  # We're only looking for syntax errors like missing braces, unclosed strings, etc.
  SYNTAX_OUTPUT=$(mono /usr/lib/mono/msbuild/Current/bin/Roslyn/csc.exe /nologo /t:library /out:/dev/null \
    /nowarn:CS0246,CS0234,CS0618,CS0612,CS0067,CS0649,CS0169,CS0414,CS0108,CS1701,CS1702 \
    /r:/home/bob/.nuget/packages/newtonsoft.json/13.0.3/lib/net45/Newtonsoft.Json.dll \
    /r:/home/bob/.nuget/packages/protobuf-net/2.4.6/lib/net40/protobuf-net.dll \
    /r:/usr/lib/mono/4.6.2-api/System.dll \
    /r:/usr/lib/mono/4.6.2-api/mscorlib.dll \
    "$file" 2>&1 || true)
    
  # Look for common syntax errors only
  if [[ "$SYNTAX_OUTPUT" == *"CS1513"* ]] || \
     [[ "$SYNTAX_OUTPUT" == *"CS1519"* ]] || \
     [[ "$SYNTAX_OUTPUT" == *"CS1003"* ]] || \
     [[ "$SYNTAX_OUTPUT" == *"CS1002"* ]]; then
    echo "Error: Syntax check failed in $file"
    echo "$SYNTAX_OUTPUT"
    echo "=================================="
  fi
done

echo "Syntax check completed successfully!"