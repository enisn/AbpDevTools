using System.Diagnostics;

namespace AbpDevTools.Environments;
public interface IProcessEnvironmentManager
{
    void SetEnvironment(string environment, string directory);
    void SetEnvironmentForProcess(string environment, ProcessStartInfo process);
    void SetEnvironmentVariablesForProcess(ProcessStartInfo process, Dictionary<string, string> variables);
}
