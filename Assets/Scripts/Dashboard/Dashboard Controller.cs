using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using System;

public class DashboardController : MonoBehaviour
{
    #region ===== Inspector References =====
    [Header("References")]
    [SerializeField] private int indexSection = 0;
    [SerializeField] private Text title;
    [SerializeField] private Text timeText;

    [Header("Selectors")]
    [SerializeField] private List<GameObject> sections = new();
    [SerializeField] private List<Button> navButtons = new();

    

    #endregion

    #region ===== Unity Lifecycle =====

    private void Awake()
    {
        if (sections.Count != navButtons.Count)
        {
            Debug.LogError("Sections and Buttons count mismatch.");
            return;
        }

        for (int i = 0; i < navButtons.Count; i++)
        {
            int index = i;
            navButtons[i].onClick.AddListener(() => ShowSection(index));
        }

    }

    private void Start()
    {
        // Set home 
        ShowSection(indexSection);
    }

    private void Update()
    {
        timeText.text = DateTime.Now.ToString("hh:mm tt");
    }

    #endregion

    #region ===== Functions =====

    private void ShowSection(int index)
    {
        for (int i = 0; i < sections.Count; i++)
        {
            sections[i].SetActive(i == index);
        }

        ChangeTitle(sections[index].name);
    #endregion
    }

    /// <summary>
    /// Changes the dashboard title
    /// </summary>
    /// <param name="text"></param>
    private void ChangeTitle(string text)
    {
        int start = text.IndexOf('(');
        int end = text.IndexOf(')');

        if (start != -1 && end != -1 && end > start) { 
            string result = text.Substring(start +1 , end - start -1);
            title.text = result;
        }
    }
}
