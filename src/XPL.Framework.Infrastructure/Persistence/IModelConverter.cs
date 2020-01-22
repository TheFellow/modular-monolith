namespace XPL.Framework.Infrastructure.Persistence
{
    public interface IModelConverter<TModel, TPersistence>
        where TPersistence : ISqlId
    {
        TModel ToModel(TPersistence persisted);
        TPersistence ToPersisted(TModel model);
        void CopyChanges(TModel from, TPersistence to);
    }
}
