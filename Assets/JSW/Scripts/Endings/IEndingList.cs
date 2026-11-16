namespace Caste.Endings
{
    /// <summary>
    /// 도감에 보여줄 엔딩 목록. 도감 화면은 이 인터페이스만 알고, 데이터가 어디서 오는지는 모른다. (DIP)
    /// 구현: EndingCatalog(인스펙터 입력), BackendEndingList(백엔드 CSV)
    /// </summary>
    public interface IEndingList
    {
        int Count { get; }
        string GetId(int index);
        bool TryGet(int index, out EndingEntry entry);
    }
}
