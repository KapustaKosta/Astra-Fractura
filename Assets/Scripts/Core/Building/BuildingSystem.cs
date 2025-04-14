using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuildingSystem : MonoBehaviour
{
    public GameObject buildingPreviewPrefab;
    private GameObject currentPreview;
    private Item currentBuildingItem;

    public void StartBuildingWithItem(Item buildingItem)
    {
        currentBuildingItem = buildingItem;
        currentPreview = Instantiate(buildingPreviewPrefab);
        // Здесь можно загрузить префаб здания из buildingItem
    }

    void Update()
    {
        if (currentPreview != null)
        {
            // Двигаем превью по курсору
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                currentPreview.transform.position = hit.point;
            }

            // ЛКМ - поставить здание
            if (Input.GetMouseButtonDown(0))
            {
                PlaceBuilding();
            }

            // ПКМ - отменить
            if (Input.GetMouseButtonDown(1))
            {
                CancelBuilding();
            }
        }
    }

    private void PlaceBuilding()
    {
        Instantiate(currentBuildingItem.buildingPrefab, currentPreview.transform.position, Quaternion.identity);
        Destroy(currentPreview);
        FindObjectOfType<Inventory>().Remove(currentBuildingItem);
    }

    private void CancelBuilding()
    {
        Destroy(currentPreview);
    }
}