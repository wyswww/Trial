using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 依赖 Unity 实际导入结果建立引用，不猜测 FBX 的本地 fileID。
public static class KayKitCharacterBinding
{
    private const string Root = "Assets/ThirdParty/KayKit/";
    private const string Output = "Assets/Generated/KayKit";
    private const string Receipt = Output + "/Binding-v1.txt";
    private const string ScenePath = "Assets/Scenes/main_scene.unity";
    private const string VisualName = "KayKitVisual";
    private static bool busy;

    [InitializeOnLoadMethod]
    private static void Schedule() => EditorApplication.delayCall += AutoBind;

    private static void AutoBind()
    {
        if (File.Exists(Receipt)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += AutoBind;
            return;
        }
        Bind();
    }

    [MenuItem("Tools/Trial/Bind KayKit Characters")]
    public static void Bind()
    {
        if (busy || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (PrefabStageUtility.GetCurrentPrefabStage() != null ||
            Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
        {
            Debug.LogWarning("[KayKit] 请先保存或关闭正在编辑的场景和 Prefab，再执行 Tools/Trial/Bind KayKit Characters。");
            return;
        }
        busy = true;
        try
        {
            EnsureFolder("Assets/Generated");
            EnsureFolder(Output);
            var knight = Prepare("Knight", "Adventurers", "Knight", "knight_texture", "sword_1handed", false);
            var warrior = Prepare("Warrior", "Skeletons", "Skeleton_Warrior", "skeleton_texture", "Skeleton_Blade", false);
            var mage = Prepare("Mage", "Skeletons", "Skeleton_Mage", "skeleton_texture", "Skeleton_Staff", true);

            // Variant 继承自近战模板，先更新基类，再明确替换其继承的外观。
            BindPrefab("Assets/Prefabs/Enemy_Melee.prefab", warrior);
            BindPrefab("Assets/Prefabs/Enemy_Ranged.prefab", mage);
            BindPrefab("Assets/Prefabs/Enemy_Ranged Variant.prefab", mage);
            BindPlayer(knight);
            File.WriteAllText(Receipt, "KayKit v1: Knight -> Player; Skeleton_Warrior -> Enemy_Melee; Skeleton_Mage -> Enemy_Ranged and Enemy_Ranged Variant.\n");
            AssetDatabase.ImportAsset(Receipt);
            Debug.Log("[KayKit] 三种角色外观、武器、材质与动画已绑定并保存。尚需在编辑器检查外观，Play Mode 未由接入工具执行。");
        }
        catch (Exception exception)
        {
            Debug.LogError("[KayKit] 接入未完成，未写入完成标记。修复错误后可从 Tools/Trial/Bind KayKit Characters 重试。\n" + exception);
        }
        finally { busy = false; }
    }

    private sealed class Character
    {
        public GameObject model, weapon, shield;
        public Material material;
        public AnimatorController controller;
        public bool ranged;
    }

    private static Character Prepare(string role, string pack, string model, string texture, string weapon, bool ranged)
    {
        string path = Root + pack + "/Characters/fbx/" + model + ".fbx";
        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null) throw new InvalidOperationException("模型尚未导入：" + path);
        bool changed = importer.animationType != ModelImporterAnimationType.Generic || !importer.importAnimation;
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.importAnimation = true;
        var clips = importer.clipAnimations;
        if (clips.Length == 0) { clips = importer.defaultClipAnimations; changed = true; }
        foreach (var clip in clips)
        {
            bool loop = clip.name == "Idle" || clip.name == "Running_A";
            if (clip.loopTime != loop) changed = true;
            clip.loopTime = loop;
        }
        if (changed) { importer.clipAnimations = clips; importer.SaveAndReimport(); }
        var clipAssets = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal)).ToArray();
        AnimationClip Find(string name) => clipAssets.FirstOrDefault(c => c.name == name)
            ?? throw new InvalidOperationException(path + " 缺少动画 " + name);

        string controllerPath = Output + "/" + role + ".controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        var machine = controller.layers[0].stateMachine;
        var states = new Dictionary<string, string>
        {
            { "Idle", "Idle" }, { "Move", "Running_A" },
            { "Attack", ranged ? "Spellcast_Shoot" : "1H_Melee_Attack_Slice_Horizontal" },
            { "Skill", "Spellcast_Raise" }, { "Dodge", "Dodge_Forward" }
        };
        foreach (var item in states)
        {
            var state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == item.Key)
                ?? machine.AddState(item.Key);
            state.motion = Find(item.Value);
            if (item.Key == "Idle") machine.defaultState = state;
            EditorUtility.SetDirty(state);
        }
        EditorUtility.SetDirty(machine);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssetIfDirty(controller);

        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + pack + "/Characters/fbx/" + texture + ".png");
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (tex == null || shader == null) throw new InvalidOperationException("缺少贴图或 URP/Lit 着色器：" + pack);
        string materialPath = Output + "/" + pack + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, materialPath); }
        material.SetTexture("_BaseMap", tex);
        material.SetColor("_BaseColor", Color.white);
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssetIfDirty(material);
        var result = new Character
        {
            model = AssetDatabase.LoadAssetAtPath<GameObject>(path),
            weapon = AssetDatabase.LoadAssetAtPath<GameObject>(Root + pack + "/Assets/fbx/" + weapon + ".fbx"),
            shield = role == "Knight" ? AssetDatabase.LoadAssetAtPath<GameObject>(Root + pack + "/Assets/fbx/shield_round.fbx") : null,
            controller = controller, material = material, ranged = ranged
        };
        if (result.model == null || result.weapon == null) throw new InvalidOperationException("缺少角色或武器：" + role);
        if (role == "Knight" && result.shield == null) throw new InvalidOperationException("缺少骑士盾牌。");
        return result;
    }

    private static void BindPrefab(string path, Character character)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Attach(root, character);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void BindPlayer(Character knight)
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool wasLoaded = scene.IsValid() && scene.isLoaded;
        Scene previous = SceneManager.GetActiveScene();
        if (!wasLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            var player = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PlayerCombat>(true)).Single();
            Attach(player.gameObject, knight);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("主场景保存失败。");
        }
        finally
        {
            if (!wasLoaded) EditorSceneManager.CloseScene(scene, true);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        }
    }

    private static void Attach(GameObject root, Character character)
    {
        var old = root.transform.Find(VisualName);
        if (old != null)
        {
            var existing = old.GetComponentInChildren<Animator>(true);
            // 已完成相同角色绑定时不重复添加模型，保留本地外观微调。
            if (old.gameObject.activeSelf && existing != null &&
                existing.runtimeAnimatorController == character.controller) return;
            // 对 Variant 的继承外观使用禁用覆盖，避免删除嵌套的继承对象。
            if (PrefabUtility.IsPartOfPrefabInstance(old.gameObject))
            {
                old.name = "KayKitInheritedVisual";
                old.gameObject.SetActive(false);
                PrefabUtility.RecordPrefabInstancePropertyModifications(old.gameObject);
            }
            else UnityEngine.Object.DestroyImmediate(old.gameObject);
        }
        var visual = (GameObject)PrefabUtility.InstantiatePrefab(character.model, root.transform);
        // 解开模型实例，允许对附带装备和骨骼子物体做本地配置。
        PrefabUtility.UnpackPrefabInstance(visual, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        visual.name = VisualName;
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;
        foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
        {
            // Knight FBX 内含多种武器/盾牌展示网格，只显示身体并挂选定武器。
            if (!renderer.name.StartsWith("Knight_", StringComparison.Ordinal) &&
                !renderer.name.StartsWith("Skeleton_", StringComparison.Ordinal))
                renderer.gameObject.SetActive(false);
            renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => character.material).ToArray();
        }
        var animator = visual.GetComponentInChildren<Animator>(true) ?? visual.AddComponent<Animator>();
        animator.runtimeAnimatorController = character.controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        // 在根对象的局部空间衡量静态身体包围盒，避免移动模型时改变物理尺寸。
        var renderers = visual.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();
        if (renderers.Length == 0) throw new InvalidOperationException("角色没有可见渲染器。");
        Bounds bounds = LocalBounds(root.transform, renderers);
        var capsule = root.GetComponent<CapsuleCollider>();
        var cc = root.GetComponent<CharacterController>();
        float height = cc != null ? cc.height : capsule != null ? capsule.height : 2f;
        float centre = cc != null ? cc.center.y : capsule != null ? capsule.center.y : 0f;
        visual.transform.localScale = Vector3.one * (height / Mathf.Max(bounds.size.y, 0.01f));
        bounds = LocalBounds(root.transform, renderers);
        visual.transform.localPosition += new Vector3(-bounds.center.x, centre - height / 2f - bounds.min.y, -bounds.center.z);

        Equip(visual, character.weapon, "handslot.r", character.material);
        if (character.shield != null) Equip(visual, character.shield, "handslot.l", character.material);
        SetLayer(visual, root.layer);

        var bridge = root.GetComponent<KayKitCharacterAnimation>() ?? root.AddComponent<KayKitCharacterAnimation>();
        bridge.Configure(animator, character.ranged);
        if (PrefabUtility.IsPartOfPrefabInstance(bridge))
            PrefabUtility.RecordPrefabInstancePropertyModifications(bridge);
        var placeholder = root.GetComponent<MeshRenderer>();
        if (placeholder != null)
        {
            placeholder.enabled = false;
            if (PrefabUtility.IsPartOfPrefabInstance(placeholder))
                PrefabUtility.RecordPrefabInstancePropertyModifications(placeholder);
        }
        // 重跑时保留用户设置的枪口；未设置时提供不依赖骨骼动画的稳定发射点。
        var ranged = root.GetComponent<RangedEnemyAI>();
        if (ranged != null)
        {
            var data = new SerializedObject(ranged);
            var muzzle = data.FindProperty("muzzle");
            if (muzzle.objectReferenceValue == null)
            {
                var point = root.transform.Find("KayKitMuzzle");
                if (point == null) { point = new GameObject("KayKitMuzzle").transform; point.SetParent(root.transform, false); }
                point.localPosition = new Vector3(0f, 0.2f, 0.6f);
                muzzle.objectReferenceValue = point;
                data.ApplyModifiedPropertiesWithoutUndo();
                if (PrefabUtility.IsPartOfPrefabInstance(ranged))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(ranged);
            }
        }
    }

    private static void Equip(GameObject visual, GameObject model, string socket, Material material)
    {
        var hand = visual.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == socket);
        if (hand == null) throw new InvalidOperationException("缺少装备挂点 " + socket);
        var weapon = (GameObject)PrefabUtility.InstantiatePrefab(model, hand);
        PrefabUtility.UnpackPrefabInstance(weapon, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        weapon.transform.localPosition = Vector3.zero;
        weapon.transform.localRotation = Quaternion.identity;
        weapon.transform.localScale = Vector3.one;
        foreach (var renderer in weapon.GetComponentsInChildren<Renderer>(true))
            renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => material).ToArray();
    }

    private static Bounds LocalBounds(Transform root, Renderer[] renderers)
    {
        var result = new Bounds();
        bool first = true;
        foreach (var renderer in renderers)
        {
            var b = renderer.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 p = root.InverseTransformPoint(b.center + Vector3.Scale(b.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                if (first) { result = new Bounds(p, Vector3.zero); first = false; }
                else result.Encapsulate(p);
            }
        }
        return result;
    }

    private static void SetLayer(GameObject root, int layer)
    {
        foreach (var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = layer;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
    }
}
