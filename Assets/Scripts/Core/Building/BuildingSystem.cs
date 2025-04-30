using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuildingSystem : MonoBehaviour
{
    public GameObject buildingPreviewPrefab; // Префаб для предпоказа
    private GameObject currentPreview; // Текущий объект предпоказа
    private Item currentBuildingItem; // Текущий выбранный предмет для строительства

    public void StartBuildingWithItem(Item buildingItem)
    {
        currentBuildingItem = buildingItem;

        // Создаем объект предпоказа
        currentPreview = Instantiate(buildingItem.buildingPrefab);
        currentPreview.transform.position = Vector3.zero; // Устанавливаем начальную позицию
    }

    void Update()
    {
        if (currentPreview != null)
        {
            // Двигаем предпоказ за курсором
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                currentPreview.transform.position = hit.point; // Обновляем позицию предпоказа
            }

            // ЛКМ - поставить здание
            if (Input.GetMouseButtonDown(0))
            {
                PlaceBuilding();
            }

            // ПКМ - отменить строительство
            if (Input.GetMouseButtonDown(1))
            {
                CancelBuilding();
            }
        }
    }

    private void PlaceBuilding()
    {
        if (currentBuildingItem == null || currentPreview == null)
        {
            Debug.LogError("Невозможно построить здание: отсутствует текущий предмет или предпоказ.");
            return;
        }

        // Создаем здание на позиции предпоказа
        Instantiate(currentBuildingItem.buildingPrefab, currentPreview.transform.position, Quaternion.identity);

        // Удаляем объект предпоказа
        Destroy(currentPreview);

        // Удаляем предмет из инвентаря
        FindObjectOfType<Inventory>().Remove(currentBuildingItem);
    }

    private void CancelBuilding()
    {
        // Отменяем строительство и удаляем предпоказ
        if (currentPreview != null)
        {
            Destroy(currentPreview);
        }
    }
}
