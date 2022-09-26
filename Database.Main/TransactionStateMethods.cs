using System.Linq;

namespace Database.Main;

public static class TransactionStateMethods
{
    public static TransactionState Upsert(MainDbContext databaseContext, string name, bool saveChanges = true)
    {
        var state = databaseContext.TransactionStates.FirstOrDefault(x => x.NAME == name);
        if ( state != null ) return state;

        state = DbHelper.GetTracked<TransactionState>(databaseContext).FirstOrDefault(x => x.NAME == name);
        if ( state != null ) return state;

        state = new TransactionState {NAME = name};

        databaseContext.TransactionStates.Add(state);
        if ( saveChanges ) databaseContext.SaveChanges();

        return state;
    }
}
