using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEngine;

public class BuildingManager : MonoBehaviour
{
    public static BuildingManager Instance;
    
    [Header("Settings")]
    public LayerMask buildableSurface;
    public Material validPlacementMaterial;
    public Material invalidPlacementMaterial;
    
    private GameObject currentBuildingPrefab;
    private GameObject buildingPreview;
    private bool isInBuildingMode = false;

    private void Awake()
    {
        Instance = this;
    }

    public void StartBuildingMode(Item buildingItem)
    {
        if (buildingItem.buildingPrefab == null)
        {
            Debug.LogError("У этого предмета нет префаба здания!");
            return;
        }

        currentBuildingPrefab = buildingItem.buildingPrefab;
        buildingPreview = Instantiate(currentBuildingPrefab);
        
        // Настройка превью
        foreach (var collider in buildingPreview.GetComponentsInChildren<Collider>())
        {
            collider.enabled = false;
        }
        
        SetPreviewMaterials(buildingPreview, validPlacementMaterial);
        isInBuildingMode = true;
    }

    private void Update()
    {
        if (!isInBuildingMode) return;

        // Движение превью за курсором
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, buildableSurface))
        {
            buildingPreview.transform.position = hit.point;
            
            // Проверка валидности позиции
            bool isValidPosition = CheckBuildPosition(hit.point);
            SetPreviewMaterials(buildingPreview, isValidPosition ? validPlacementMaterial : invalidPlacementMaterial);
            
            // ЛКМ - построить
            if (Input.GetMouseButtonDown(0) && isValidPosition)
            {
                Build(hit.point);
            }
        }

        // ПКМ - отмена
        if (Input.GetMouseButtonDown(1))
        {
            CancelBuilding();
        }
    }

    private bool CheckBuildPosition(Vector3 position)
    {
        // Проверка коллизий и других ограничений
        Collider[] colliders = Physics.OverlapBox(position, currentBuildingPrefab.GetComponent<BoxCollider>().size / 2);
        return colliders.Length == 0;
    }

    private void Build(Vector3 position)
    {
        if (Inventory.Instance.selectedItem != null && 
            Inventory.Instance.selectedItem.itemType == ItemType.Building)
        {
            Instantiate(currentBuildingPrefab, position, Quaternion.identity);
            Inventory.Instance.Remove(Inventory.Instance.selectedItem); // Удаляем предмет из инвентаря
            ExitBuildingMode();
        }
    }

    public void CancelBuilding()
    {
        ExitBuildingMode();
    }

    private void ExitBuildingMode()
    {
        if (buildingPreview != null)
        {
            Destroy(buildingPreview);
        }
        isInBuildingMode = false;
    }

    private void SetPreviewMaterials(GameObject target, Material material)
    {
        foreach (var renderer in target.GetComponentsInChildren<Renderer>())
        {
            var materials = new Material[renderer.sharedMaterials.Length];
            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = material;
            }
            renderer.sharedMaterials = materials;
        }
    }
}