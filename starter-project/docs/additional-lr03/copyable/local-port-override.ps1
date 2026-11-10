$env:ConnectionStrings__SecureLab = (
  'Host=127.0.0.1;Port=54330;Database=securelab;' +
  'Username=securelab;Password=local-study-password;' +
  'Include Error Detail=false'
)
try {
  dotnet run --project src/SecureLab.Api
} finally {
  Remove-Item Env:ConnectionStrings__SecureLab
}
