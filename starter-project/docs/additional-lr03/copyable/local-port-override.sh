connection='Host=127.0.0.1;Port=54330;Database=securelab;'
connection+='Username=securelab;Password=local-study-password;'
connection+='Include Error Detail=false'
ConnectionStrings__SecureLab="$connection" \
  dotnet run --project src/SecureLab.Api
unset connection
