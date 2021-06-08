var Student = (function () {
    function Student(firstName, secondName, lastName) {
        this.firstName = firstName;
        this.secondName = secondName;
        this.lastName = lastName;
        this.fullName = firstName + " " + secondName + " " + lastName;
    }
    return Student;
})();

function greeter(user) {
    return "Hello," + user.firstName + " " + user.lastName;
}

var user = new Student("Li", "Yi", "Han");
document.body.innerHTML = greeter(user);
//# sourceMappingURL=greeter.js.map
