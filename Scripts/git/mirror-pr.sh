#!/bin/bash
set -e

HELPSTR="Usage: $( basename $0 ) <PR number> [remote to push to] [GitHub repo to pull from] [GitHub repo to create a PR in]
For example: $( basename $0 ) 1002 floofstation space-wizards/space-station-14 fansana/floofstation1

This script will cherry-pick a pull request from a GitHub repo into a new branch and make a pull request for you.

Default pull remote is 'upstream', default GitHub repo is 'simple-station/einstein-engines', default GitHub repo to create a PR in is 'fansana/floofstation1'"

PRNUM=$4 # First arg to a shell script
if [[ -z $PRNUM || $PRNUM == "--help" || $PRNUM == "-h" ]]; then
    echo "$HELPSTR"
    exit 1
fi
PUSH_REMOTE=${1:-upstream}
PULL_REPO=${2:-simple-station/einstein-engines}
PUSH_REPO=${3:-fansana/floofstation1}

echo "Pulling from $PULL_REPO and pushing to $PUSH_REMOTE, then creating a PR in $PUSH_REPO"

pr_info=$(gh pr view "https://github.com/${PULL_REPO}/pull/$PRNUM" --json number,title,headRefName,mergeCommit,body)
merge_commit=$(echo "$pr_info" | jq -r .mergeCommit.oid)
title=$(echo "$pr_info" | jq -r .title)
body=$(echo "$pr_info" | jq -r .body)

echo "Creating PR for: $title"


target_branch="upstream/$PRNUM"
current_branch=$(git rev-parse --abbrev-ref HEAD)
if [ "$current_branch" != "$target_branch" ]; then
    echo "Resetting current branch"
    git checkout master
    git pull "$PUSH_REMOTE" master

    parent_count=$(git rev-list --parents -n 1 "$merge_commit" | wc -w)

    echo "Creating branch"
    git checkout -b upstream/$PRNUM

    # If this commit has two parents, pick the first one. This is not a safe way to do it, TODO: prompt the user?
    cherry_pick_cmd="git cherry-pick"
    [ "$parent_count" -gt 2 ] && cherry_pick_cmd+=" -m 1"

    if ! $cherry_pick_cmd $merge_commit; then
        echo "Resolve the merge conflicts manually and hit enter."
        read -r
        git cherry-pick --continue --no-edit --allow-empty
    fi
    git push --set-upstream $PUSH_REMOTE
fi

if [ -f .git/CHERRY_PICK_HEAD ]; then
    echo "Trying to finish the cherry-pick..."
    git cherry-pick --continue --no-edit || exit 1;
fi

echo "Type in any optional description you want to appear in the PR. You can add line breaks by typing \n."
read -r opt_description
opt_description=$( echo -n "$opt_description" ) # Process escape sequences

body=$(echo "$pr_info" | jq -r .body)
body1=$(echo "$body" | sed 's/:cl:/:cl: EinsteinEngines/g')
body2=$(
    printf "Upstream PR: https://github.com/%s/pull/%s\n\n" "$PULL_REPO" "$PRNUM"
    echo "$opt_description"
    echo
    echo "-----"
    echo
    echo "$body1"
)

echo "PR description is as follows:"
echo "$body2"
echo "Press enter to proceed or CTRL+C to cancel."
read

git push "$PUSH_REMOTE"
gh pr create --repo "$PUSH_REPO" --base master --head "upstream/$PRNUM" --title "Upstream Merge #$PRNUM | $title" --body "$body2"
