using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Hisab;
public sealed class RelayCommand(Action action) : ICommand
{
    public bool CanExecute(object? parameter)=>true;
    public void Execute(object? parameter)=>action();
    public event EventHandler? CanExecuteChanged {add{} remove{}}
}
public sealed class MainViewModel(Store store) : INotifyPropertyChanged
{
    public Store Store {get;}=store;
    public AccountingService Accounting {get;}=new(store);
    public bool Arabic { get; private set; }=store.Setting("language","ar")=="ar";
    public void RefreshPreferences()=>Arabic=Store.Setting("language","ar")=="ar";
    string route="home",status="";
    public string Route {get=>route;set{route=value;OnChanged();}}
    public string Status {get=>status;set{status=value;OnChanged();}}
    public string T(string ar,string en)=>Arabic?ar:en;
    public event PropertyChangedEventHandler? PropertyChanged;
    void OnChanged([CallerMemberName]string? name=null)=>PropertyChanged?.Invoke(this,new(name));
}
