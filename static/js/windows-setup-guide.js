(() => {
  const guide = document.querySelector("[data-windows-setup-guide]");

  if (!guide) {
    return;
  }

  const outputs = {
    admin: guide.querySelector("#windows-setup-admin-command-output"),
    user: guide.querySelector("#windows-setup-user-command-output"),
  };
  const copyButtons = Array.from(guide.querySelectorAll("[data-copy-commands]"));
  const copyStatuses = {
    admin: guide.querySelector('[data-copy-status="admin"]'),
    user: guide.querySelector('[data-copy-status="user"]'),
  };
  const selectedCount = guide.querySelector("[data-selected-count]");
  const commandCount = guide.querySelector("[data-command-count]");
  const storageKey = "mikael.dev:windows-dev-setup:selected-steps:v6";

  const steps = Array.from(guide.querySelectorAll("[data-step]")).map(
    (element) => {
      const checkbox = element.querySelector("[data-step-toggle]");
      const commandLines = Array.from(
        element.querySelectorAll("[data-command-line]"),
      )
        .map((line) => line.textContent.trim())
        .filter(Boolean);
      const title =
        element
          .querySelector(".windows-setup__step-title")
          ?.textContent.trim() || checkbox.value;

      return {
        checkbox,
        commandLines,
        element,
        id: checkbox.value,
        requiresAdmin: element.dataset.requiresAdmin === "true",
        title,
      };
    },
  );

  const readStoredSelection = () => {
    try {
      const stored = window.localStorage.getItem(storageKey);

      if (!stored) {
        return null;
      }

      const parsed = JSON.parse(stored);

      if (
        !Array.isArray(parsed) ||
        parsed.some((value) => typeof value !== "string")
      ) {
        window.localStorage.removeItem(storageKey);
        console.warn(
          "Removed invalid Windows setup guide selection from localStorage.",
        );
        return null;
      }

      return parsed;
    } catch (error) {
      console.warn("Could not read Windows setup guide selection.", error);
      return null;
    }
  };

  const persistSelection = () => {
    const selectedIds = steps
      .filter((step) => step.checkbox.checked)
      .map((step) => step.id);

    try {
      window.localStorage.setItem(storageKey, JSON.stringify(selectedIds));
    } catch (error) {
      console.warn("Could not save Windows setup guide selection.", error);
    }
  };

  const renderCommands = () => {
    const linesByTarget = {
      admin: [],
      user: [],
    };

    steps.forEach((step) => {
      step.element.dataset.selected = String(step.checkbox.checked);

      if (!step.checkbox.checked || step.commandLines.length === 0) {
        return;
      }

      const target = step.requiresAdmin ? "admin" : "user";
      const lines = linesByTarget[target];

      if (lines.length > 0) {
        lines.push("");
      }

      lines.push(`# ${step.title}`, ...step.commandLines);
    });

    Object.entries(linesByTarget).forEach(([target, lines]) => {
      outputs[target].textContent =
        lines.length > 0
          ? `${lines.join("\n")}\n`
          : `# No ${target === "admin" ? "admin" : "user"} commands selected.\n`;
    });

    selectedCount.textContent = String(
      steps.filter((step) => step.checkbox.checked).length,
    );
    commandCount.textContent = String(
      steps
        .filter((step) => step.checkbox.checked)
        .reduce((total, step) => total + step.commandLines.length, 0),
    );

    persistSelection();
  };

  const setCopyStatus = (target, message) => {
    copyStatuses[target].textContent = message;
  };

  const copyWithFallback = (text) => {
    const textarea = document.createElement("textarea");
    textarea.value = text;
    textarea.setAttribute("readonly", "");
    textarea.style.position = "fixed";
    textarea.style.top = "0";
    textarea.style.left = "0";
    textarea.style.opacity = "0";
    document.body.appendChild(textarea);
    textarea.select();

    const copied = document.execCommand("copy");
    textarea.remove();

    if (!copied) {
      throw new Error("The browser rejected the copy command.");
    }
  };

  const copyCommands = async (target) => {
    const text = outputs[target].textContent.trim();

    if (!text || text.startsWith("# No ")) {
      setCopyStatus(target, "No commands selected.");
      return;
    }

    try {
      if (navigator.clipboard && window.isSecureContext) {
        await navigator.clipboard.writeText(`${text}\n`);
      } else {
        copyWithFallback(`${text}\n`);
      }

      setCopyStatus(target, "Commands copied.");
    } catch (error) {
      console.warn("Could not copy Windows setup commands.", error);
      setCopyStatus(
        target,
        "Copy failed. Select the command box and copy manually.",
      );
    }
  };

  const storedSelection = readStoredSelection();

  if (storedSelection) {
    const selectedIds = new Set(storedSelection);
    steps.forEach((step) => {
      step.checkbox.checked = selectedIds.has(step.id);
    });
  }

  steps.forEach((step) => {
    step.checkbox.addEventListener("change", renderCommands);
  });

  copyButtons.forEach((button) => {
    button.addEventListener("click", () =>
      copyCommands(button.dataset.copyCommands),
    );
  });
  renderCommands();
})();
