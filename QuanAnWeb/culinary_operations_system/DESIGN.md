---
name: Culinary Operations System
colors:
  surface: '#f9f9f9'
  surface-dim: '#dadada'
  surface-bright: '#f9f9f9'
  surface-container-lowest: '#ffffff'
  surface-container-low: '#f3f3f3'
  surface-container: '#eeeeee'
  surface-container-high: '#e8e8e8'
  surface-container-highest: '#e2e2e2'
  on-surface: '#1a1c1c'
  on-surface-variant: '#5b4139'
  inverse-surface: '#2f3131'
  inverse-on-surface: '#f1f1f1'
  outline: '#8f7068'
  outline-variant: '#e4beb4'
  surface-tint: '#b02e00'
  primary: '#ac2d00'
  on-primary: '#ffffff'
  primary-container: '#d53e0b'
  on-primary-container: '#fffbff'
  inverse-primary: '#ffb5a0'
  secondary: '#546067'
  on-secondary: '#ffffff'
  secondary-container: '#d7e4ec'
  on-secondary-container: '#5a666d'
  tertiary: '#00685c'
  on-tertiary: '#ffffff'
  tertiary-container: '#198374'
  on-tertiary-container: '#f4fffb'
  error: '#ba1a1a'
  on-error: '#ffffff'
  error-container: '#ffdad6'
  on-error-container: '#93000a'
  primary-fixed: '#ffdbd1'
  primary-fixed-dim: '#ffb5a0'
  on-primary-fixed: '#3b0900'
  on-primary-fixed-variant: '#872100'
  secondary-fixed: '#d7e4ec'
  secondary-fixed-dim: '#bbc8d0'
  on-secondary-fixed: '#111d23'
  on-secondary-fixed-variant: '#3c494f'
  tertiary-fixed: '#97f3e2'
  tertiary-fixed-dim: '#7ad7c6'
  on-tertiary-fixed: '#00201b'
  on-tertiary-fixed-variant: '#005047'
  background: '#f9f9f9'
  on-background: '#1a1c1c'
  surface-variant: '#e2e2e2'
typography:
  display-lg:
    fontFamily: Inter
    fontSize: 48px
    fontWeight: '700'
    lineHeight: 56px
    letterSpacing: -0.02em
  headline-lg:
    fontFamily: Inter
    fontSize: 32px
    fontWeight: '600'
    lineHeight: 40px
    letterSpacing: -0.01em
  headline-md:
    fontFamily: Inter
    fontSize: 24px
    fontWeight: '600'
    lineHeight: 32px
  headline-sm:
    fontFamily: Inter
    fontSize: 20px
    fontWeight: '600'
    lineHeight: 28px
  body-lg:
    fontFamily: Inter
    fontSize: 18px
    fontWeight: '400'
    lineHeight: 28px
  body-md:
    fontFamily: Inter
    fontSize: 16px
    fontWeight: '400'
    lineHeight: 24px
  body-sm:
    fontFamily: Inter
    fontSize: 14px
    fontWeight: '400'
    lineHeight: 20px
  label-lg:
    fontFamily: Inter
    fontSize: 14px
    fontWeight: '600'
    lineHeight: 20px
    letterSpacing: 0.01em
  label-md:
    fontFamily: Inter
    fontSize: 12px
    fontWeight: '600'
    lineHeight: 16px
    letterSpacing: 0.04em
  mono-label:
    fontFamily: Courier Prime
    fontSize: 14px
    fontWeight: '400'
    lineHeight: 20px
rounded:
  sm: 0.125rem
  DEFAULT: 0.25rem
  md: 0.375rem
  lg: 0.5rem
  xl: 0.75rem
  full: 9999px
spacing:
  base: 8px
  xs: 4px
  sm: 8px
  md: 16px
  lg: 24px
  xl: 32px
  gutter: 16px
  margin-mobile: 16px
  margin-desktop: 32px
---

## Brand & Style
The design system focuses on a **Corporate / Modern** aesthetic tailored for high-pressure restaurant environments. It balances the high-energy urgency of hospitality with the structured reliability of enterprise SaaS. The interface prioritizes clarity, speed of recognition, and task-oriented density.

The style leverages a **card-based layout** with clear data segmentation to assist three distinct user mentalities:
*   **Admin:** Analytical and data-heavy.
*   **Cashier/POS:** High-speed, high-touch interactions requiring large hit targets.
*   **Kitchen/Bar:** High-contrast, glanceable status updates for low-light or steam-heavy environments.

## Colors
This design system utilizes a **Terracotta (#E64A19)** primary color to stimulate appetite and drive action. The **Navy Blue (#263238)** secondary color provides the institutional "management" weight necessary for financial and administrative screens.

**Functional Color Logic:**
*   **Primary (Terracotta):** Reserved for primary actions (Place Order, Process Payment) and urgent notifications.
*   **Secondary (Navy):** Used for navigation sidebars, headers, and structural elements to provide a sense of stability.
*   **Neutrals:** A range of soft grays is used to distinguish between different "zones" on the screen (e.g., a darker gray for the background and pure white for interactive cards).
*   **Semantic Status:** Specific shades for table management—Green for available, Terracotta for occupied, and Amber for cleaning.

## Typography
The system uses **Inter** for its exceptional legibility at small sizes and high "x-height," which is critical for reading order lists and inventory spreadsheets. 

For POS and Kitchen interfaces, use `label-lg` or `headline-sm` for item modifiers to ensure they are not missed. `mono-label` (using Courier Prime) is introduced specifically for "Kitchen Chit" simulations and receipt previews to provide a clear visual distinction between digital interface text and printed physical output.

## Layout & Spacing
The design system employs a **Fluid Grid** model with an 8px base unit. 

*   **Admin Dashboard:** Uses a 12-column grid with 24px gutters to allow for dense data visualization and multi-column forms.
*   **POS Interface:** Uses a fixed-aspect ratio grid for "Item Tiles," ensuring touch targets are never smaller than 64x64px.
*   **Kitchen Display System (KDS):** Uses a horizontal-scrolling "Columnar" layout where each column represents an active order ticket. 

**Breakpoints:**
*   **Mobile (<600px):** Single column, bottom-anchored primary actions.
*   **Tablet (600px - 1024px):** Optimized for landscape touch-use; split-screen view for "Order Entry" (Menu on left, Cart on right).
*   **Desktop (>1024px):** Full administrative view with persistent left-hand navigation sidebar.

## Elevation & Depth
Depth is conveyed through **Tonal Layering** and **Soft Ambient Shadows**. 

*   **Level 0 (Background):** Soft gray (#F5F5F5).
*   **Level 1 (Cards):** Pure white surfaces with a subtle 1px border (#E0E0E0) and a soft, wide-spread shadow (Y: 2, Blur: 8, Opacity: 0.05).
*   **Level 2 (Active/Hover):** Increased shadow depth (Y: 4, Blur: 12, Opacity: 0.1) to indicate interactivity.
*   **Level 3 (Modals/Overlays):** High-contrast shadows and a 40% opacity Navy Blue backdrop blur to pull the user's focus to critical tasks like "Close Bill."

## Shapes
A **Soft (0.25rem)** roundedness is applied to buttons and input fields to maintain a professional, systematic feel. However, **Cards** and **Table Tiles** use `rounded-lg` (0.5rem) to provide a more modern, friendly container for groups of information. 

**Kitchen Tickets** use a unique "perforated" bottom edge visual treatment to distinguish them from interactive UI cards.

## Components
### Buttons
*   **Primary:** Solid Terracotta with white text. High-contrast.
*   **Secondary:** Navy Blue outlines with Navy text. Used for "Cancel" or "Print Duplicate."
*   **Ghost:** Transparent background with Navy text. Used for utility actions like "Add Note."

### Table Status Indicators
Tables in the floor plan view must display:
*   **Occupied:** Solid Terracotta border with timer label.
*   **Cleaning:** Pulse animation on an Amber border.
*   **Reserved:** Dashed Navy Blue border.

### Chips & Tags
Used for dietary restrictions (e.g., "GF", "Vegan"). Use a background-fill of 10% opacity of the category color with a 100% opacity text of the same color for high legibility without visual clutter.

### Input Fields
Standardized height of 48px for Admin and 56px for POS (touch-friendly). Labels are always top-aligned; never use floating labels to ensure maximum clarity during fast-paced data entry.

### POS Item Tiles
Tiles must include the item name (`label-lg`) and price (`label-md`). If an item is "Out of Stock," the tile should be at 50% opacity with a diagonal "Sold Out" banner.