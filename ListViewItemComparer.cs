using System.Collections;
using System.Collections.Generic;
using System.Windows.Forms;

class ListViewItemComparer : IComparer {
    private int         mColumn;
    private SortOrder   mSortOrder;
    private Dictionary<ListViewItem, ArcItem> mItemData;
    
    public void Init(Dictionary<ListViewItem, ArcItem> itemData) {
        mItemData = itemData;
    }
    public void ApplySort (int column, SortOrder sortOrder) {
        mColumn = column;
        mSortOrder = sortOrder;
    }
    public int Compare (object a, object b) {
        var itemA = (ListViewItem)a;
        var itemB = (ListViewItem)b;
        int returnVal = 0;

        if (mColumn == 1) {
            var date1 = mItemData[itemA].mTimeStamp;
            var date2 = mItemData[itemB].mTimeStamp;
            returnVal = date1.CompareTo(date2);
        }
        else if (mColumn == 2) {
            int size1 = mItemData[itemA].mFileSize;
            int size2 = mItemData[itemB].mFileSize;
            returnVal = size1.CompareTo(size2);
        }
        else {
            string str1 = itemA.SubItems[mColumn].Text;
            string str2 = itemB.SubItems[mColumn].Text;
            returnVal = string.Compare(str1, str2);
        }
                
        return mSortOrder == SortOrder.Ascending ? returnVal : -returnVal;
    }
}